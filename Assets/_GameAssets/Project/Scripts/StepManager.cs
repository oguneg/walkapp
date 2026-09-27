using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// Turns the device pedometer into game steps.
///
/// Live:    while the app is open, the Input System StepCounter is polled once a second and the delta is credited.
/// Offline: on launch and on every resume, the steps taken since the last credited point are credited exactly once
///          ("sync"), then the "while you were away" popup reports where they went.
///
/// Everything credited so far corresponds to (liveBaseline, lastSyncUtc). Those only move forward when steps are
/// credited, and are what gets persisted on pause - never a fresh sensor read, which can be 0 while the sensor wakes up.
///
/// Android's step counter is cumulative since boot, so the offline amount is a counter delta.
/// iOS's counter restarts at 0 every time it is enabled, so the offline amount comes from StepHistory (CMPedometer).
/// </summary>
public class StepManager : MonoSingleton<StepManager>
{
    [SerializeField] private TextMeshProUGUI totalStepText, offlineStepText, sessionStepText, readStepText;
    [SerializeField] private int offlinePopupThreshold = 100;

    public long totalSteps { get; private set; }
    public long readSteps { get; private set; }
    public long offlineSteps { get; private set; } // credited by the most recent sync
    public long sessionSteps { get; private set; } // credited live since launch

    /// <summary>True once the startup sync is done and live counting runs.</summary>
    public bool IsReady { get; private set; }

    /// <summary>
    /// False while a launch/resume sync hasn't credited the away steps yet.
    /// Deadline checks (express jobs) wait for this so steps walked before a deadline aren't missed.
    /// </summary>
    public bool StepsSettled => !isSyncing && (IsReady || Time.realtimeSinceStartup > SettleGiveUpSeconds);

    private const string LastHardwareStepsKey = "LastHardwareSteps";
    private const string LastSyncUnixKey = "LastStepSyncUnix";
    private const string TotalStepsKey = "TotalSteps";
    private const string ActivityRecognition = "android.permission.ACTIVITY_RECOGNITION";

    private const float SensorWakeTimeout = 10f;
    private const float ResumeReadTimeout = 1.5f;
    private const float HistoryTimeout = 3f;
    private const float SettleGiveUpSeconds = 30f;
    private const int MaxPlausibleStepsPerMinute = 200;

#if UNITY_IOS && !UNITY_EDITOR
    private static readonly bool CounterIsCumulative = false;
#else
    private static readonly bool CounterIsCumulative = true;
#endif

    private long liveBaseline;
    private bool hasBaseline;
    private DateTime lastSyncUtc;
    private bool hasSyncPoint;
    private bool isSyncing;

    public override void Init()
    {
        totalSteps = PlayerPrefs.GetInt(TotalStepsKey, 0);
        hasBaseline = PlayerPrefs.HasKey(LastHardwareStepsKey);
        liveBaseline = PlayerPrefs.GetInt(LastHardwareStepsKey, 0);
        hasSyncPoint = PlayerPrefs.HasKey(LastSyncUnixKey + "_lowBits");
        if (hasSyncPoint) lastSyncUtc = GameClock.FromUnix(PlayerPrefsX.GetLong(LastSyncUnixKey));
#if UNITY_EDITOR
        editorCounter = PlayerPrefsX.GetLong(EditorCounterKey, 10_000);
#endif
    }

    private IEnumerator Start()
    {
        // Let every other Start() run first: steps must not be credited before the saved job and currencies are loaded.
        yield return null;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(ActivityRecognition))
            Permission.RequestUserPermission(ActivityRecognition);

        while (!Permission.HasUserAuthorizedPermission(ActivityRecognition))
            yield return new WaitForSecondsRealtime(1f);
#endif

#if !UNITY_EDITOR
        while (StepCounter.current == null)
            yield return null;

        InputSystem.EnableDevice(StepCounter.current);

        // The sensor can take a few seconds to hand over its first value.
        float waited = 0f;
        while (!TryReadCounter(out _) && waited < SensorWakeTimeout)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (waited >= SensorWakeTimeout)
            Debug.LogWarning("Step sensor gave no reading yet. Proceeding; live counting starts when it does.");

        RecordingApiManager.instance.InitializeRecordingAPI();
#endif

        yield return SyncRoutine(isStartup: true);
        IsReady = true;
        StartCoroutine(LivePollRoutine());
    }

    private void OnApplicationPause(bool isPaused)
    {
        // Unity also calls OnApplicationPause(false) right after Awake. The startup sync in Start() covers that,
        // and nothing may be persisted before it ran (the sensor isn't trustworthy yet).
        if (!IsReady) return;

        if (isPaused) SaveState();
        else StartCoroutine(SyncRoutine(isStartup: false));
    }

    private void OnApplicationQuit()
    {
        if (IsReady) SaveState();
    }

    private void OnDestroy()
    {
        if (StepCounter.current != null)
            InputSystem.DisableDevice(StepCounter.current);
    }

    // ---------------------------------------------------------------- live

    private IEnumerator LivePollRoutine()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            yield return wait;
            if (isSyncing || !TryReadCounter(out long reading)) continue;

            readSteps = reading;
            // Lower than the baseline = the counter restarted (re-enable on iOS). Nothing to credit, just rebase.
            long delta = reading >= liveBaseline ? reading - liveBaseline : 0;

            // Every step up to this reading is accounted for, so the sync point moves even when idle.
            // Keeps "away for" honest and the next history window tight.
            SetBaseline(reading);
            lastSyncUtc = GameClock.UtcNow;
            hasSyncPoint = true;

            if (delta > 0) CreditLive(delta);
        }
    }

    private void CreditLive(long steps)
    {
        sessionSteps += steps;
        totalSteps += steps;
#if UNITY_EDITOR
        StepHistory.EditorRecord(GameClock.UtcNow.AddSeconds(-1), GameClock.UtcNow, steps);
#endif
        JobManager.instance.AllocateSteps(steps);
        ExpressJobManager.instance.OnLiveSteps(steps);
        UpdateGUI();
    }

    // ---------------------------------------------------------------- offline sync

    private IEnumerator SyncRoutine(bool isStartup)
    {
        if (isSyncing) yield break;
        isSyncing = true;

        // Resume only: re-registering the listener makes Android flush its batched steps (and restarts iOS's counter).
        if (!isStartup && StepCounter.current != null)
        {
            InputSystem.DisableDevice(StepCounter.current);
            yield return null;
            InputSystem.EnableDevice(StepCounter.current);
        }

        // Wait for a trustworthy reading. On resume, prefer one that differs from the pre-pause value (fresh data).
        long reading = 0;
        bool hasReading = false;
        float waited = 0f;
        float timeout = isStartup ? 0f : ResumeReadTimeout;
        do
        {
            hasReading = TryReadCounter(out reading);
            if (hasReading && (isStartup || !CounterIsCumulative || reading != liveBaseline)) break;
            waited += Time.unscaledDeltaTime;
            yield return null;
        } while (waited < timeout);

        DateTime now = GameClock.UtcNow;
        DateTime from = hasSyncPoint ? lastSyncUtc : now;
        long offline = 0;

        if (CounterIsCumulative)
        {
            // A saved baseline of 0 came from an unready sensor (older builds saved those) - crediting against it
            // would hand out every step since boot. Treat it like a first run instead.
            if (hasReading && hasBaseline && liveBaseline > 0)
                offline = reading >= liveBaseline ? reading - liveBaseline : reading; // lower = rebooted, count since boot

            // Sensor glitches aside, nobody averages more than a running cadence while away.
            if (hasSyncPoint && now > from)
                offline = Math.Min(offline, (long)((now - from).TotalMinutes * MaxPlausibleStepsPerMinute) + 100);
        }
        else if (hasSyncPoint && StepHistory.IsSupported)
        {
            long? history = null;
            bool done = false;
            StepHistory.QuerySteps(from, now, result =>
            {
                history = result;
                done = true;
            });

            float historyWait = 0f;
            while (!done && historyWait < HistoryTimeout)
            {
                historyWait += Time.unscaledDeltaTime;
                yield return null;
            }

            offline = history ?? 0;
        }

        if (hasReading) SetBaseline(reading);
        if (hasReading || !CounterIsCumulative)
        {
            // Only advance the sync point when the steps up to "now" were actually accounted for.
            lastSyncUtc = now;
            hasSyncPoint = true;
        }

        // Not persisted here on purpose: the baseline is saved on pause together with the bank, job and
        // currencies it paid for. After a crash both roll back and the same steps are simply credited again.
        isSyncing = false;

        CreditOffline(offline, from, now, showPopup: true);
    }

    private void CreditOffline(long steps, DateTime fromUtc, DateTime toUtc, bool showPopup)
    {
        offlineSteps = steps;
        if (steps <= 0)
        {
            UpdateGUI();
            return;
        }

        totalSteps += steps;
        StepAllocation allocation = JobManager.instance.AllocateSteps(steps);
        ExpressJobManager.instance.OnOfflineSteps(steps, fromUtc, toUtc);
        UpdateGUI();

        if (showPopup && steps >= offlinePopupThreshold)
        {
            TimeSpan? awayFor = toUtc > fromUtc ? toUtc - fromUtc : (TimeSpan?)null;
            var report = new OfflineStepReport(allocation, awayFor);
            PopupManager.instance.EnqueuePopup(PopupType.PopupOfflineSteps,
                popup => ((PopupOfflineSteps)popup).Initialize(report));
        }
    }

    // ---------------------------------------------------------------- counter + persistence

    private bool TryReadCounter(out long value)
    {
#if UNITY_EDITOR
        value = editorCounter;
        return true;
#else
        var counter = StepCounter.current;
        if (counter == null || !counter.enabled)
        {
            value = 0;
            return false;
        }

        value = counter.stepCounter.ReadValue();
        // Android reports 0 until the sensor delivers its first event. A real cumulative reading is never 0 once
        // anyone has walked since boot, so treat 0 as "not ready" rather than risk rebasing to it.
        return !CounterIsCumulative || value > 0;
#endif
    }

    private void SetBaseline(long reading)
    {
        liveBaseline = reading;
        hasBaseline = true;
    }

    private void SaveState()
    {
        if (hasBaseline) PlayerPrefs.SetInt(LastHardwareStepsKey, (int)liveBaseline);
        if (hasSyncPoint) PlayerPrefsX.SetLong(LastSyncUnixKey, GameClock.ToUnix(lastSyncUtc));
        PlayerPrefs.SetInt(TotalStepsKey, (int)Math.Min(totalSteps, int.MaxValue));
#if UNITY_EDITOR
        PlayerPrefsX.SetLong(EditorCounterKey, editorCounter);
#endif
        PlayerPrefs.Save();
    }

    private void UpdateGUI()
    {
        if (totalStepText) totalStepText.text = $"{totalSteps:N0}";
        if (sessionStepText) sessionStepText.text = $"{sessionSteps:N0}";
        if (readStepText) readStepText.text = $"{readSteps:N0}";
        if (offlineStepText) offlineStepText.text = $"{offlineSteps:N0}";
    }

    // ---------------------------------------------------------------- debug (SRDebugger)

#if UNITY_EDITOR
    private const string EditorCounterKey = "EditorSimulatedStepCounter";
    private long editorCounter;
#endif

    /// <summary>Walk with the app open. In the Editor this moves the simulated sensor; on device it credits directly.</summary>
    public void DebugWalk(int steps)
    {
#if UNITY_EDITOR
        editorCounter += steps;
#else
        CreditLive(steps);
#endif
    }

    /// <summary>
    /// Pretend the app was closed for <paramref name="minutes"/> while walking <paramref name="steps"/>:
    /// the game clock jumps forward, and the steps arrive through the same code path as a real resume.
    /// </summary>
    public void DebugSimulateAway(int steps, int minutes)
    {
        if (!IsReady || isSyncing) return;

        DateTime from = GameClock.UtcNow;
        GameClock.DebugAdvance(TimeSpan.FromMinutes(minutes));
        DateTime to = GameClock.UtcNow;
#if UNITY_EDITOR
        StepHistory.EditorRecord(from, to, steps);
        editorCounter += steps;
        if (CounterIsCumulative)
        {
            StartCoroutine(SyncRoutine(isStartup: false));
            return;
        }
#endif
        // On device the OS knows nothing about these steps, so hand them over directly.
        lastSyncUtc = to;
        hasSyncPoint = true;
        CreditOffline(steps, from, to, showPopup: true);
    }
}
