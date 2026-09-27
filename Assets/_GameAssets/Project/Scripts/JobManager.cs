using System;
using System.Collections;
using System.Collections.Generic;
using OgunWorks.UI;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class JobManager : MonoSingleton<JobManager>
{
    /// <summary>One kind of regular job. Bigger jobs of a kind get proportionally more time.</summary>
    [Serializable]
    public class JobTier
    {
        public JobType type;
        [Tooltip("Distance in km, picked in steps of distanceStep. A job needs StepsPerKm steps per km.")]
        public int minDistance = 15, maxDistance = 50, distanceStep = 1;
        [Tooltip("Time limit (hours) for the shortest and the longest job of this type.")]
        public float minDeadlineHours = 1f, maxDeadlineHours = 2f;
    }

    public const int StepsPerKm = 3;

    [Header("Job types and deadlines")]
    [Tooltip("Every regular job has a deadline. Express jobs (ExpressJobManager) are the short, hardcore ones.")]
    [SerializeField] private JobTier[] tiers =
    {
        new JobTier { type = JobType.Short, minDistance = 15, maxDistance = 50, distanceStep = 1, minDeadlineHours = 1f, maxDeadlineHours = 2f },
        new JobTier { type = JobType.Medium, minDistance = 150, maxDistance = 490, distanceStep = 10, minDeadlineHours = 4f, maxDeadlineHours = 12f },
        new JobTier { type = JobType.Long, minDistance = 900, maxDistance = 4000, distanceStep = 100, minDeadlineHours = 120f, maxDeadlineHours = 168f },
    };

    private const float HistoryTimeout = 3f;
    private bool resolvingDeadline;

    public List<JobData> availableJobs;
    public ActiveJobSaveData activeJob;
    public int completedJobCount = 0;
    private UIManager uiManager;
    private CurrencyManager currencyManager;
    private UpgradeManager upgradeManager;
    private ExperienceManager experienceManager;

    [Header("Burn rate")]
    [Tooltip("Max burn rate before upgrades. Burn Rate Booster upgrades add to it.")]
    [SerializeField] private float baseMaxBurnRate = 3f;
    [Tooltip("Up to this rate, banked steps convert 1:1.")]
    [SerializeField] private float lossFreeBurnRate = 2f;
    [Tooltip("Loss at one step above the loss-free rate (0.10 = 10% at 3x). Efficient Burner upgrades scale it down.")]
    [SerializeField] private float baseLossSlope = 0.10f;
    [Tooltip("Loss grows with (rate - lossFreeRate) ^ this, so each extra x costs more than the last.")]
    [SerializeField] private float lossExponent = 1.5f;

    private const string BurnRateKey = "BurnRate";
    private float burnRate = -1f;

    /// <summary>
    /// How fast banked steps are spent: at rate R every walked step also pulls R-1 steps of progress from the bank.
    /// 1x keeps the bank, 2x (default) is loss-free, above that the bank pays extra for the same progress.
    /// </summary>
    public float BurnRate
    {
        get
        {
            if (burnRate < 0) burnRate = PlayerPrefs.GetFloat(BurnRateKey, lossFreeBurnRate);
            return SnapBurnRate(burnRate);
        }
        set
        {
            burnRate = SnapBurnRate(value);
            PlayerPrefs.SetFloat(BurnRateKey, burnRate);
            uiManager.UpdateActiveJobStatus();
        }
    }

    public float MaxBurnRate => baseMaxBurnRate + upgradeManager.Get(UpgradeType.BurnRateMax);

    public const float BurnRateStep = 0.5f;

    /// <summary>Burn rate stops: 1x (off, the bank is untouched), then the loss-free rate, then +0.5 up to the max.</summary>
    public int BurnRateStopCount => 2 + Mathf.Max(0, Mathf.FloorToInt((MaxBurnRate - lossFreeBurnRate) / BurnRateStep + 1e-3f));

    public float BurnRateAtStop(int stop) =>
        stop <= 0 ? 1f : Mathf.Min(MaxBurnRate, lossFreeBurnRate + (stop - 1) * BurnRateStep);

    public int BurnRateStop(float rate) =>
        rate < 1.5f ? 0 : 1 + Mathf.Max(0, Mathf.RoundToInt((rate - lossFreeBurnRate) / BurnRateStep));

    // Older saves may hold in-between values like 2.3x: move them onto the nearest stop.
    private float SnapBurnRate(float rate) =>
        BurnRateAtStop(Mathf.Clamp(BurnRateStop(rate), 0, BurnRateStopCount - 1));

    /// <summary>Extra share of banked steps spent at this rate, e.g. 0.10 = pulling 2,000 costs 2,200.</summary>
    public float BurnLoss(float rate)
    {
        if (rate <= lossFreeBurnRate) return 0f;
        return baseLossSlope * upgradeManager.Get(UpgradeType.BurnLoss) * Mathf.Pow(rate - lossFreeBurnRate, lossExponent);
    }

    private void Awake()
    {
        uiManager = UIManager.instance;
        currencyManager = CurrencyManager.instance;
        upgradeManager = UpgradeManager.instance;
        experienceManager = ExperienceManager.instance;
    }

    private IEnumerator Start()
    {
        CreateJob();
        activeJob = JobSaveManager.LoadJob();
        if (activeJob != null && activeJob.jobData == null) activeJob = null;
        if (activeJob != null)
        {
            MigrateLoadedJob(activeJob);
            DisplayActiveJob();
        }
        else
        {
            uiManager.ClearActiveJob();
        }

        StartCoroutine(DeadlineRoutine());

        while (true)
        {
            CreateJob();
            yield return new WaitForSeconds(5f);
        }
    }

    private void CreateJob()
    {
        JobTier tier = tiers[Random.Range(0, tiers.Length)];
        var job = new JobData();
        job.cargoType = (CargoType)Random.Range(0, 8);
        job.jobType = tier.type;
        int step = Mathf.Max(1, tier.distanceStep);
        job.distance = tier.minDistance + Random.Range(0, (tier.maxDistance - tier.minDistance) / step + 1) * step;
        job.steps = job.distance * StepsPerKm;
        job.timeInMinutes = DeadlineMinutes(job);

        job.fuelCost = job.distance * 10 * Random.Range(10, 15);
        job.experience = job.distance * 10;
        job.reward = job.distance * Random.Range(10, 15) / 3;
        var fleet = FleetManager.instance;
        job.reward = (long)(job.reward * upgradeManager.Get(UpgradeType.IncomeMultiplier) * fleet.RewardMultiplier);
        job.fuelCost = (long)(job.fuelCost / upgradeManager.Get(UpgradeType.FuelEfficiency) * fleet.FuelMultiplier);
        uiManager.AddJob(job);
    }

    /// <summary>Time limit for a job: its type's range, scaled by where its distance sits in that type's range.</summary>
    public int DeadlineMinutes(JobData job)
    {
        JobTier tier = Array.Find(tiers, t => t.type == job.jobType) ?? tiers[0];
        float t = tier.maxDistance > tier.minDistance
            ? Mathf.InverseLerp(tier.minDistance, tier.maxDistance, job.distance)
            : 0f;
        float minutes = Mathf.Lerp(tier.minDeadlineHours, tier.maxDeadlineHours, t) * 60f;

        // Round to numbers people read at a glance: 15 min steps under 3h, hours under 2 days, then half days.
        float unit = minutes < 180f ? 15f : minutes < 2880f ? 60f : 720f;
        return Mathf.Max(15, Mathf.RoundToInt(minutes / unit) * (int)unit);
    }

    /// <summary>One job at a time: no regular job and no express job (running or waiting to be claimed).</summary>
    public bool JobSlotFree => activeJob == null && !ExpressJobManager.instance.HasJob;

    public bool AcceptJob(JobData job)
    {
        if (!JobSlotFree) return false;
        activeJob = new ActiveJobSaveData(job);
        Debug.Log(activeJob);
        JobSaveManager.SaveJob(activeJob);
        DisplayActiveJob();
        uiManager.ForceTab(TabType.ActiveJobs);
        return true;
    }

    private void DisplayActiveJob()
    {
        uiManager.DisplayActiveJob(activeJob);
    }

    public void EndJob(bool isSuccess)
    {
        if (activeJob == null) return;
        if (isSuccess && activeJob.state == JobState.Claimable)
        {
            completedJobCount++;
            experienceManager.AddExperience(activeJob.jobData.distance * 10);
            currencyManager.AddCurrency(CurrencyType.Coin, DailyBonusManager.instance.ApplyBonus(activeJob.jobData.reward));
            uiManager.UpdateCompletedJobCount(completedJobCount);
        }

        activeJob = null;
        JobSaveManager.ClearJob();
    }

    public void RegisterSteps(int amount) => AllocateSteps(amount);

    public StepAllocation AllocateSteps(long steps) => AllocateSteps(steps, GameClock.UnixNow);

    /// <summary>
    /// Single place walked steps are spent, for both live and offline steps:
    /// the running job first (each walked step also pulls BurnRate-1 steps of progress from the bank while it lasts,
    /// paying the burn loss on top), then the step bank up to its cap. Whatever doesn't fit is reported as overflow.
    /// The job only takes steps walked by its deadline: <paramref name="walkedByUnix"/> is when these steps were walked
    /// (the end of the window they come from).
    /// </summary>
    public StepAllocation AllocateSteps(long steps, long walkedByUnix)
    {
        var result = new StepAllocation { total = Math.Max(0, steps) };
        long remaining = result.total;
        if (remaining == 0) return result;

        var job = activeJob;
        if (job != null && job.IsRunning && job.stepsLeft > 0 && walkedByUnix <= job.deadlineUnix)
        {
            StepAllocation used = ApplyToJob(remaining);
            remaining -= used.toJob;
            result.toJob = used.toJob;
            result.bankBurned = used.bankBurned;
            result.bankCost = used.bankCost;

            if (job.stepsLeft <= 0) Deliver();
            else uiManager.UpdateActiveJobStatus();
        }

        if (remaining > 0)
        {
            long space = currencyManager.GetCurrencyCap(CurrencyType.BankedStep) -
                         currencyManager.GetCurrencyAmount(CurrencyType.BankedStep);
            long deposit = Math.Clamp(remaining, 0, Math.Max(0, space));
            if (deposit > 0) currencyManager.AddCurrency(CurrencyType.BankedStep, deposit);
            result.toBank = deposit;
            result.overflow = remaining - deposit;
        }

        return result;
    }

    /// <summary>Moves the running job with up to <paramref name="walkedAvailable"/> walked steps plus the bank burn they pull.</summary>
    private StepAllocation ApplyToJob(long walkedAvailable)
    {
        var result = new StepAllocation();
        long need = activeJob.stepsLeft;
        if (need <= 0 || walkedAvailable <= 0) return result;

        long bank = currencyManager.GetCurrencyAmount(CurrencyType.BankedStep);
        double extraPerStep = BurnRate - 1.0;             // progress pulled from the bank per walked step
        double costPerExtra = 1.0 + BurnLoss(BurnRate);   // bank spent per step of that progress

        long walked, extra;
        if (extraPerStep <= 0 || bank <= 0)
        {
            walked = Math.Min(walkedAvailable, need);
            extra = 0;
        }
        else
        {
            // Walked steps the bank can boost fully, and walked steps needed to finish.
            long boostable = (long)Math.Floor(bank / (extraPerStep * costPerExtra));
            long toFinishBoosted = (long)Math.Ceiling(need / (1.0 + extraPerStep));
            long walkedToFinish = boostable >= toFinishBoosted
                ? toFinishBoosted
                : boostable + (need - (long)Math.Floor(boostable * (1.0 + extraPerStep)));
            walked = Math.Min(walkedAvailable, walkedToFinish);
            extra = Math.Min(need - walked, (long)Math.Floor(Math.Min(walked, boostable) * extraPerStep));
        }

        // The epsilon keeps float noise (0.1f = 0.1000000015) from rounding 2,200 up to 2,201.
        long cost = Math.Min(bank, (long)Math.Ceiling(extra * costPerExtra - 1e-4));

        activeJob.stepsLeft -= walked + extra;
        activeJob.walkedSteps += walked;
        result.toJob = walked;
        result.bankBurned = extra;
        result.bankCost = cost;

        if (cost > 0) currencyManager.AddCurrency(CurrencyType.BankedStep, -cost);
        return result;
    }

    // ---------------------------------------------------------------- deadlines

    private void Deliver()
    {
        activeJob.stepsLeft = 0;
        activeJob.state = JobState.Claimable;
        uiManager.UpdateActiveJobStatus();
    }

    private void Fail()
    {
        activeJob.state = JobState.Failed;
        AudioManager.instance.PlaySound(SoundType.Fail);
        uiManager.UpdateActiveJobStatus();
    }

    // While the app is open: settle the job once its deadline passes. Waits for a launch/resume sync so steps
    // walked before the deadline are credited first.
    private IEnumerator DeadlineRoutine()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            yield return wait;
            if (activeJob != null && activeJob.IsRunning && activeJob.IsPastDeadline && StepManager.instance.StepsSettled)
                yield return ResolveDeadline();
        }
    }

    /// <summary>
    /// The deadline passed with the job still running. Before failing it, ask the OS how many steps were really
    /// walked between accepting and the deadline: steps walked in time but credited late (sensor batching, the app
    /// being killed) still count, pulling from the bank at the burn rate like any other step. If that covers the
    /// job it's delivered and waits to be claimed; otherwise it's failed.
    /// </summary>
    public IEnumerator ResolveDeadline()
    {
        var job = activeJob;
        if (job == null || !job.IsRunning || !job.IsPastDeadline || resolvingDeadline) yield break;
        resolvingDeadline = true;

        if (job.stepsLeft > 0 && StepHistory.IsSupported)
        {
            long? walked = null;
            bool done = false;
            StepHistory.QuerySteps(job.AcceptUtc, job.DeadlineUtc, result =>
            {
                walked = result;
                done = true;
            });

            float waited = 0f;
            while (!done && waited < HistoryTimeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            // Leftover steps aren't banked here: late-credited steps may already be in the bank.
            if (activeJob == job && job.IsRunning && walked.HasValue && walked.Value > job.walkedSteps)
                ApplyToJob(walked.Value - job.walkedSteps);
        }

        resolvingDeadline = false;
        if (activeJob != job || !job.IsRunning) yield break;

        if (job.stepsLeft <= 0) Deliver();
        else Fail();
    }

    // Jobs saved before deadlines existed get a fresh time limit for their type.
    private void MigrateLoadedJob(ActiveJobSaveData job)
    {
        if (job.deadlineUnix <= 0)
        {
            job.acceptUnix = GameClock.UnixNow;
            job.deadlineUnix = job.acceptUnix + DeadlineMinutes(job.jobData) * 60L;
            job.walkedSteps = Math.Max(0, job.jobData.steps - job.stepsLeft);
        }

        if (job.state != JobState.Active && job.state != JobState.Claimable && job.state != JobState.Failed)
            job.state = job.stepsLeft <= 0 ? JobState.Claimable : JobState.Active;
        else if (job.state == JobState.Active && job.stepsLeft <= 0)
            job.state = JobState.Claimable;
    }

    /// <summary>SRDebugger: move the deadline to now, so the next check settles the job.</summary>
    public void DebugExpireDeadline()
    {
        if (activeJob != null && activeJob.IsRunning) activeJob.deadlineUnix = GameClock.UnixNow;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && activeJob != null)
        {
            JobSaveManager.SaveJob(activeJob);
        }
    }

    void OnApplicationQuit()
    {
        if (activeJob != null)
        {
            JobSaveManager.SaveJob(activeJob);
        }
    }
}