using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Daily stars and momentum.
/// Each local day, walking past each milestone (3,000 / 6,000 / 10,000 / 12,500 / 15,000) earns a star, max 5.
/// Momentum is the stars of the last 7 days (today + 6 before, max 35). The window slides: nothing ever resets,
/// the oldest day just drops off as today fills in, so one lazy day costs a little bonus, not a streak.
/// Coin payouts get +bonusPerStar per star up to starCap (30); stars above the cap are a safety net.
///
/// Per-day steps come from the OS step history when available (so days the app wasn't opened still count and
/// steps land on the day they were walked), otherwise from live steps plus offline steps spread over the away time.
/// </summary>
public class DailyBonusManager : MonoSingleton<DailyBonusManager>
{
    [SerializeField] private int[] milestones = { 3000, 6000, 10000, 12500, 15000 };
    [SerializeField] private int windowDays = 7;
    [Tooltip("Stars above this still count toward momentum but add no more bonus (safety net).")]
    [SerializeField] private int starCap = 30;
    [Tooltip("Coin bonus per star, e.g. 0.02 = +2% per star, +60% at the cap.")]
    [SerializeField] private float bonusPerStar = 0.02f;
    [SerializeField] private float historyRefreshSeconds = 300f;

    /// <summary>Steps, stars or the day changed.</summary>
    public event Action OnChanged;
    /// <summary>(new stars, stars today) when today's milestones are passed.</summary>
    public event Action<int, int> OnStarsEarnedToday;

    public IReadOnlyList<int> Milestones => milestones;
    public int MaxStarsPerDay => milestones.Length;
    public int WindowDays => windowDays;
    public int StarCap => starCap;
    public int MaxWindowStars => milestones.Length * windowDays;
    public float BonusPerStar => bonusPerStar;

    [Serializable]
    private class DayEntry
    {
        public int day;   // local yyyymmdd
        public long steps;
    }

    [Serializable]
    private class SaveData
    {
        public List<DayEntry> days = new List<DayEntry>();
        public int celebratedDay;
        public int celebratedStars;
    }

    private const string SaveKey = "DailyBonusState";
    private SaveData data;
    private bool refreshing;
    private int lastTodayKey;

    public override void Init() => Load();

    private IEnumerator Start()
    {
        // History needs the step sync to have run once (Recording API init, sensor ready).
        while (!StepManager.instance.StepsSettled) yield return null;
        lastTodayKey = TodayKey;
        RefreshFromHistory();
        AfterChange();

        var wait = new WaitForSeconds(60f);
        float sinceRefresh = 0f;
        while (true)
        {
            yield return wait;
            sinceRefresh += 60f;
            if (TodayKey != lastTodayKey || sinceRefresh >= historyRefreshSeconds)
            {
                lastTodayKey = TodayKey;
                sinceRefresh = 0f;
                RefreshFromHistory();
                AfterChange();
            }

            Save();
        }
    }

    // ---------------------------------------------------------------- queries

    public static int DayKey(DateTime local) => local.Year * 10000 + local.Month * 100 + local.Day;
    public DateTime TodayLocal => GameClock.UtcNow.ToLocalTime().Date;
    public int TodayKey => DayKey(TodayLocal);

    public long StepsOn(DateTime localDate)
    {
        int key = DayKey(localDate);
        foreach (var d in data.days)
            if (d.day == key) return d.steps;
        return 0;
    }

    public int StarsFor(long steps)
    {
        int stars = 0;
        foreach (int m in milestones)
            if (steps >= m) stars++;
        return stars;
    }

    public long TodaySteps => StepsOn(TodayLocal);
    public int TodayStars => StarsFor(TodaySteps);

    /// <summary>Next milestone today, or -1 when all are done.</summary>
    public long NextMilestone
    {
        get
        {
            long today = TodaySteps;
            foreach (int m in milestones)
                if (today < m) return m;
            return -1;
        }
    }

    public int WindowStars
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < windowDays; i++) sum += StarsFor(StepsOn(TodayLocal.AddDays(-i)));
            return sum;
        }
    }

    /// <summary>Stars of the oldest day in the window: what drops off tomorrow.</summary>
    public int StarsDroppingTomorrow => StarsFor(StepsOn(TodayLocal.AddDays(-(windowDays - 1))));

    public int BonusStars => Math.Min(WindowStars, starCap);
    public float BonusPercent => BonusStars * bonusPerStar;
    public float MaxBonusPercent => starCap * bonusPerStar;

    /// <summary>Coins after the momentum bonus.</summary>
    public long ApplyBonus(long coins) =>
        ProgressionManager.instance.IsUnlocked(Feature.DailyStars) ? (long)Math.Round(coins * (1.0 + BonusPercent)) : coins;

    // ---------------------------------------------------------------- step intake (from StepManager)

    public void OnLiveSteps(long steps)
    {
        if (steps <= 0) return;
        AddToDay(TodayKey, steps);
        AfterChange();
    }

    public void OnOfflineSteps(long steps, DateTime fromUtc, DateTime toUtc)
    {
        if (steps <= 0) return;

        // The OS knows which day each step was walked on; spreading would guess.
        if (StepHistory.IsSupported)
        {
            RefreshFromHistory();
            return;
        }

        // No history: spread the steps over the local days the away time covered, by time share.
        DateTime from = fromUtc.ToLocalTime();
        DateTime to = toUtc.ToLocalTime();
        double total = (to - from).TotalSeconds;
        if (total <= 0)
        {
            AddToDay(TodayKey, steps);
        }
        else
        {
            long assigned = 0;
            for (DateTime day = from.Date; day <= to.Date; day = day.AddDays(1))
            {
                DateTime start = day > from ? day : from;
                DateTime end = day.AddDays(1) < to ? day.AddDays(1) : to;
                if (end <= start) continue;
                long share = end.Date == to.Date && end == to
                    ? steps - assigned // last slice takes the rounding remainder
                    : (long)Math.Round(steps * (end - start).TotalSeconds / total);
                assigned += share;
                AddToDay(DayKey(day), share);
            }
        }

        AfterChange();
    }

    /// <summary>Re-read each day of the window from the OS and keep the larger of history and what we counted.</summary>
    public void RefreshFromHistory()
    {
        if (refreshing || !StepHistory.IsSupported) return;
        refreshing = true;

        DateTime now = GameClock.UtcNow;
        int pending = windowDays;
        for (int i = 0; i < windowDays; i++)
        {
            DateTime day = TodayLocal.AddDays(-i);
            DateTime startUtc = day.ToUniversalTime();
            DateTime endUtc = day.AddDays(1).ToUniversalTime();
            if (endUtc > now) endUtc = now;
            int key = DayKey(day);

            StepHistory.QuerySteps(startUtc, endUtc, result =>
            {
                if (result.HasValue) SetDayAtLeast(key, result.Value);
                if (--pending > 0) return;
                refreshing = false;
                AfterChange();
            });
        }
    }

    // ---------------------------------------------------------------- internals

    private DayEntry Entry(int key)
    {
        foreach (var d in data.days)
            if (d.day == key) return d;
        var entry = new DayEntry { day = key };
        data.days.Add(entry);
        return entry;
    }

    private void AddToDay(int key, long steps) => Entry(key).steps += steps;

    private void SetDayAtLeast(int key, long steps)
    {
        var entry = Entry(key);
        if (steps > entry.steps) entry.steps = steps;
    }

    private void AfterChange()
    {
        // Keep one extra day so tomorrow's "drops off" hint still has data.
        int oldest = DayKey(TodayLocal.AddDays(-windowDays));
        data.days.RemoveAll(d => d.day < oldest);

        int today = TodayKey;
        if (data.celebratedDay != today)
        {
            data.celebratedDay = today;
            data.celebratedStars = 0;
        }

        int stars = TodayStars;
        if (stars > data.celebratedStars)
        {
            int gained = stars - data.celebratedStars;
            data.celebratedStars = stars;
            Save();
            OnStarsEarnedToday?.Invoke(gained, stars);
        }

        OnChanged?.Invoke();
    }

    private void Load()
    {
        data = null;
        if (PlayerPrefs.HasKey(SaveKey))
        {
            try
            {
                data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            }
            catch (Exception e)
            {
                Debug.LogError($"Daily bonus: failed to load. {e.Message}");
            }
        }

        if (data == null) data = new SaveData();
        if (data.days == null) data.days = new List<DayEntry>();
    }

    private void Save() => PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));

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

    /// <summary>Fill the 6 days before today with the given steps each (to see momentum and the drop-off).</summary>
    public void DebugFillPastDays(long stepsPerDay)
    {
        for (int i = 1; i < windowDays; i++) Entry(DayKey(TodayLocal.AddDays(-i))).steps = stepsPerDay;
        AfterChange();
    }

    public void DebugReset()
    {
        data = new SaveData();
        AfterChange();
    }
}
