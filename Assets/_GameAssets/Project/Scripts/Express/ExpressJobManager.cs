using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Express deliveries: occasional time-limited offers ("walk 4,000 steps in the next hour") that pay several times
/// a regular job. The hardcore jobs: a short accept window and a deadline measured in minutes, where regular jobs
/// get hours to days. They run alongside the regular job (the same walked steps count for both) and banked steps
/// can't be used: only steps walked inside the window count. Offers sit on top of the Job List; an accepted job
/// shows on the Active tab until it's claimed or its miss is acknowledged.
///
/// While the app is open, progress comes from live steps. Time spent closed is counted from the OS step history
/// (StepHistory), which knows exactly how many of those steps fell before the deadline. Without history
/// (Editor fallback, unsupported devices) offline steps are pro-rated by how much of the away time overlapped.
/// </summary>
public class ExpressJobManager : MonoSingleton<ExpressJobManager>
{
    [Serializable]
    public class Tuning
    {
        [Tooltip("Minutes between an express job ending (or an offer expiring) and the next offer.")]
        public Vector2Int offerIntervalMinutes = new Vector2Int(45, 120);
        [Tooltip("The first offer comes quickly so players discover the feature.")]
        public int firstOfferDelayMinutes = 2;
        [Tooltip("How long an offer waits to be accepted.")]
        public int acceptWindowMinutes = 15;
        [Tooltip("Possible time limits. Repeat a value to make it more likely.")]
        public int[] durationsMinutes = { 30, 45, 60, 60, 90 };
        [Tooltip("Target pace. ~100 steps/min is a brisk walk, so 50-75 means walking most of the window.")]
        public Vector2 stepsPerMinute = new Vector2(50f, 75f);
        [Tooltip("Coins per target step before the income multiplier. Regular jobs pay ~1.1-1.7.")]
        public Vector2 coinsPerStep = new Vector2(4f, 6f);
        public float experiencePerStep = 5f;
        [Tooltip("Offers only appear between these local hours. Nobody should be sent out walking at 3am.")]
        public int activeHourStart = 8;
        public int activeHourEnd = 22;
        [Tooltip("While a job runs, how often the OS step history is re-read (seconds).")]
        public float historyRefreshSeconds = 60f;
        [Tooltip("Coins to call the dispatcher for an offer right now. Doubles with every call on the same day.")]
        public long dispatcherBaseCost = 2500;
        [Tooltip("Dispatcher Network upgrades can't shorten the wait between offers below this share.")]
        public float minIntervalMultiplier = 0.4f;
    }

    [SerializeField] private Tuning tuning = new Tuning();

    /// <summary>Offer appeared/expired, progress moved, status changed.</summary>
    public event Action OnChanged;
    /// <summary>Once a second, for countdown labels.</summary>
    public event Action OnTick;
    public event Action<ExpressOffer> OnOfferCreated;

    public bool HasOffer => data.hasOffer;
    public ExpressOffer Offer => data.hasOffer ? data.offer : null;
    public bool OfferSeen => data.offerSeen;
    public bool HasJob => data.hasJob;
    public ExpressJob Job => data.hasJob ? data.job : null;
    public DateTime NextOfferUtc => GameClock.FromUnix(data.nextOfferUnix);
    public int CompletedCount => data.completedCount;
    public int FailedCount => data.failedCount;

    /// <summary>A dispatcher call is possible when nothing is offered or running.</summary>
    public bool CanCallDispatcher => !data.hasOffer && !data.hasJob;
    public long DispatcherCost => tuning.dispatcherBaseCost << Math.Min(10, DispatcherCallsToday);
    private int DispatcherCallsToday => data.dispatchDay == TodayKey ? data.dispatchCalls : 0;
    private static int TodayKey
    {
        get
        {
            DateTime local = GameClock.UtcNow.ToLocalTime();
            return local.Year * 10000 + local.Month * 100 + local.Day;
        }
    }

    private bool IsRunning => data.hasJob && data.job.status == ExpressStatus.Active;

    private const string SaveKey = "ExpressJobState";
    private ExpressSaveData data;
    private float historyTimer;
    private bool historyQueryInFlight;
    private bool historyQueryQueued;
    private bool historyFailed;

    public override void Init() => Load();

    private IEnumerator Start()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            yield return wait;
            Tick();
        }
    }

    private void Tick()
    {
        long now = GameClock.UnixNow;

        if (data.hasOffer && now >= data.offer.expiresUnix)
        {
            data.hasOffer = false;
            ScheduleNextOffer();
            SaveAndNotify();
        }

        if (!data.hasOffer && !data.hasJob && now >= data.nextOfferUnix)
            CreateOffer();

        if (IsRunning)
        {
            if (now >= data.job.deadlineUnix)
            {
                TryResolve();
            }
            else if (StepHistory.IsSupported)
            {
                historyTimer += 1f;
                if (historyTimer >= tuning.historyRefreshSeconds)
                {
                    historyTimer = 0f;
                    RefreshHistory();
                }
            }
        }

        OnTick?.Invoke();
    }

    // ---------------------------------------------------------------- offers

    private void CreateOffer()
    {
        long now = GameClock.UnixNow;
        int duration = tuning.durationsMinutes[Random.Range(0, tuning.durationsMinutes.Length)];
        float pace = Random.Range(tuning.stepsPerMinute.x, tuning.stepsPerMinute.y);
        int target = Mathf.Max(500, Mathf.RoundToInt(duration * pace / 100f) * 100);
        var upgrades = UpgradeManager.instance;
        float income = upgrades.Get(UpgradeType.IncomeMultiplier) * upgrades.Get(UpgradeType.ExpressIncome) *
                       FleetManager.instance.RewardMultiplier;

        data.offer = new ExpressOffer
        {
            cargoType = (CargoType)Random.Range(0, Enum.GetValues(typeof(CargoType)).Length),
            targetSteps = target,
            durationMinutes = duration,
            reward = (long)(target * Random.Range(tuning.coinsPerStep.x, tuning.coinsPerStep.y) * income),
            experience = (long)(target * tuning.experiencePerStep),
            createdUnix = now,
            expiresUnix = now + tuning.acceptWindowMinutes * 60L
        };
        data.hasOffer = true;
        data.offerSeen = false;
        SaveAndNotify();
        OnOfferCreated?.Invoke(data.offer);
    }

    public bool AcceptOffer()
    {
        if (!data.hasOffer || data.hasJob) return false;

        long now = GameClock.UnixNow;
        if (now >= data.offer.expiresUnix) return false;

        data.job = new ExpressJob
        {
            offer = data.offer,
            acceptUnix = now,
            deadlineUnix = now + data.offer.durationMinutes * 60L,
            status = ExpressStatus.Active
        };
        data.hasJob = true;
        data.hasOffer = false;
        historyTimer = 0f;
        historyFailed = false;
        SaveAndNotify();
        return true;
    }

    public void DeclineOffer()
    {
        if (!data.hasOffer) return;
        data.hasOffer = false;
        ScheduleNextOffer();
        SaveAndNotify();
    }

    public void MarkOfferSeen()
    {
        if (!data.hasOffer || data.offerSeen) return;
        data.offerSeen = true;
        Save();
    }

    private void ScheduleNextOffer()
    {
        float frequency = Mathf.Max(tuning.minIntervalMultiplier, UpgradeManager.instance.Get(UpgradeType.ExpressFrequency));
        float minutes = Random.Range(tuning.offerIntervalMinutes.x, tuning.offerIntervalMinutes.y + 1) * frequency;
        data.nextOfferUnix = ClampToActiveHours(GameClock.UnixNow + (long)(minutes * 60f));
    }

    /// <summary>Pay to get an offer right now instead of waiting (a coin sink). Price doubles per call each day.</summary>
    public bool CallDispatcher()
    {
        if (!CanCallDispatcher) return false;

        long cost = DispatcherCost;
        if (!CurrencyManager.instance.CanAfford(CurrencyType.Coin, cost)) return false;

        CurrencyManager.instance.AddCurrency(CurrencyType.Coin, -cost);
        int today = TodayKey;
        data.dispatchCalls = data.dispatchDay == today ? data.dispatchCalls + 1 : 1;
        data.dispatchDay = today;
        CreateOffer();
        return true;
    }

    private long ClampToActiveHours(long unix)
    {
        DateTime local = GameClock.FromUnix(unix).ToLocalTime();
        if (local.Hour >= tuning.activeHourStart && local.Hour < tuning.activeHourEnd) return unix;

        DateTime morning = local.Date.AddHours(tuning.activeHourStart);
        if (local.Hour >= tuning.activeHourEnd) morning = morning.AddDays(1);
        morning = morning.AddMinutes(Random.Range(0, 60));
        return GameClock.ToUnix(morning.ToUniversalTime());
    }

    // ---------------------------------------------------------------- steps

    public void OnLiveSteps(long steps)
    {
        if (!IsRunning || steps <= 0) return;
        if (GameClock.UnixNow >= data.job.deadlineUnix) return; // too late, the deadline check will resolve it

        data.job.liveSteps += steps;
        UpdateProgress();
    }

    /// <summary>Steps walked while the app was closed, somewhere in [fromUtc, toUtc].</summary>
    public void OnOfflineSteps(long steps, DateTime fromUtc, DateTime toUtc)
    {
        if (!IsRunning || steps <= 0) return;

        var job = data.job;
        long from = GameClock.ToUnix(fromUtc);
        long to = GameClock.ToUnix(toUtc);
        long overlap = Math.Min(to, job.deadlineUnix) - Math.Max(from, job.acceptUnix);
        if (overlap <= 0) return;

        double share = to > from ? Math.Min(1.0, (double)overlap / (to - from)) : 1.0;
        job.offlineEstimate += (long)Math.Round(steps * share);

        if (StepHistory.IsSupported) RefreshHistory(); // exact split instead of the estimate
        else UpdateProgress();
    }

    private void RefreshHistory()
    {
        if (!data.hasJob) return;
        if (historyQueryInFlight)
        {
            historyQueryQueued = true;
            return;
        }

        var job = data.job;
        long end = Math.Min(GameClock.UnixNow, job.deadlineUnix);
        long liveMark = job.liveSteps;
        historyQueryInFlight = true;

        StepHistory.QuerySteps(job.AcceptUtc, GameClock.FromUnix(end), result =>
        {
            historyQueryInFlight = false;
            if (!data.hasJob || data.job != job) return;

            historyFailed = !result.HasValue;
            if (result.HasValue)
            {
                job.historySteps = result.Value;
                job.liveAtHistory = liveMark;
            }

            UpdateProgress();

            if (historyQueryQueued)
            {
                historyQueryQueued = false;
                RefreshHistory();
            }
        });
    }

    private void UpdateProgress()
    {
        if (!data.hasJob) return;

        var job = data.job;
        long progress = job.liveSteps;
        if (job.historySteps >= 0)
            progress = Math.Max(progress, job.historySteps + (job.liveSteps - job.liveAtHistory));
        if (!StepHistory.IsSupported || (historyFailed && job.historySteps < 0))
            progress = Math.Max(progress, job.liveSteps + job.offlineEstimate);

        job.bestProgress = Math.Max(job.bestProgress, progress);

        if (job.status == ExpressStatus.Active && job.bestProgress >= job.offer.targetSteps)
            Finish(job.bestProgress);
        else
            OnChanged?.Invoke();
    }

    // ---------------------------------------------------------------- resolution

    private void TryResolve()
    {
        // Right after launch/resume the away steps may not be credited yet. Deciding now could fail a job
        // whose steps are about to arrive.
        if (!StepManager.instance.StepsSettled) return;

        var job = data.job;
        job.status = ExpressStatus.Resolving;
        OnChanged?.Invoke();

        if (!StepHistory.IsSupported)
        {
            Finish(Math.Max(job.bestProgress, job.liveSteps + job.offlineEstimate));
            return;
        }

        StepHistory.QuerySteps(job.AcceptUtc, job.DeadlineUtc, result =>
        {
            if (!data.hasJob || data.job != job || job.status != ExpressStatus.Resolving) return;

            long final = result.HasValue
                ? Math.Max(job.bestProgress, result.Value)
                : Math.Max(job.bestProgress, job.liveSteps + job.offlineEstimate);
            Finish(final);
        });
    }

    private void Finish(long finalSteps)
    {
        var job = data.job;
        job.finalSteps = finalSteps;
        job.bestProgress = Math.Max(job.bestProgress, finalSteps);
        job.status = finalSteps >= job.offer.targetSteps ? ExpressStatus.Completed : ExpressStatus.Failed;
        AudioManager.instance.PlaySound(job.status == ExpressStatus.Completed ? SoundType.Success : SoundType.Fail);
        SaveAndNotify();
    }

    public void Claim()
    {
        if (!data.hasJob || data.job.status != ExpressStatus.Completed) return;

        CurrencyManager.instance.AddCurrency(CurrencyType.Coin, DailyBonusManager.instance.ApplyBonus(data.job.offer.reward));
        ExperienceManager.instance.AddExperience(data.job.offer.experience);
        data.completedCount++;
        EndJob();
    }

    /// <summary>Acknowledge a failed job.</summary>
    public void Dismiss()
    {
        if (!data.hasJob || data.job.status != ExpressStatus.Failed) return;
        data.failedCount++;
        EndJob();
    }

    public void Abandon()
    {
        if (!IsRunning) return;
        data.failedCount++;
        EndJob();
    }

    private void EndJob()
    {
        data.hasJob = false;
        ScheduleNextOffer();
        SaveAndNotify();
    }

    // ---------------------------------------------------------------- persistence

    private void Load()
    {
        data = null;
        if (PlayerPrefs.HasKey(SaveKey))
        {
            try
            {
                data = JsonUtility.FromJson<ExpressSaveData>(PlayerPrefs.GetString(SaveKey));
            }
            catch (Exception e)
            {
                Debug.LogError($"Express: failed to load state. {e.Message}");
            }
        }

        if (data == null)
            data = new ExpressSaveData { nextOfferUnix = GameClock.UnixNow + tuning.firstOfferDelayMinutes * 60L };

        // Killed mid-resolution: just resolve again.
        if (data.hasJob && data.job.status == ExpressStatus.Resolving)
            data.job.status = ExpressStatus.Active;
    }

    private void Save() => PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));

    private void SaveAndNotify()
    {
        Save();
        OnChanged?.Invoke();
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused) return;
        Save();
        PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        Save();
        PlayerPrefs.Save();
    }

    // ---------------------------------------------------------------- debug (SRDebugger)

    public void DebugSpawnOffer()
    {
        if (data.hasJob) return;
        CreateOffer();
    }

    public void DebugEndWindowNow()
    {
        if (IsRunning) data.job.deadlineUnix = GameClock.UnixNow;
    }

    public void DebugReset()
    {
        data = new ExpressSaveData { nextOfferUnix = GameClock.UnixNow + 60 };
        SaveAndNotify();
    }
}
