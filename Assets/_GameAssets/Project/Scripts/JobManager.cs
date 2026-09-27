using System;
using System.Collections;
using System.Collections.Generic;
using OgunWorks.UI;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class JobManager : MonoSingleton<JobManager>
{
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
            return Mathf.Clamp(burnRate, 1f, MaxBurnRate);
        }
        set
        {
            burnRate = Mathf.Clamp(Mathf.Round(value * 10f) / 10f, 1f, MaxBurnRate);
            PlayerPrefs.SetFloat(BurnRateKey, burnRate);
            uiManager.UpdateActiveJobStatus();
        }
    }

    public float MaxBurnRate => baseMaxBurnRate + upgradeManager.Get(UpgradeType.BurnRateMax);

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
        if (activeJob != null)
        {
            DisplayActiveJob();
        }
        else
        {
            uiManager.ClearActiveJob();
        }

        while (true)
        {
            CreateJob();
            yield return new WaitForSeconds(5f);
        }
    }

    private void CreateJob()
    {
        var job = new JobData();
        job.cargoType = (CargoType)Random.Range(0, 8);
        job.jobType = (JobType)Random.Range(0, 3);
        switch (job.jobType)
        {
            case JobType.Short:
                job.distance = Random.Range(15, 51);
                job.steps = job.distance * 3;
                job.timeInMinutes = job.steps / 10;
                break;
            case JobType.Medium:
                job.distance = Random.Range(15, 50) * 10;
                job.steps = job.distance * 3;
                job.timeInMinutes = Random.Range(2, 7) * 30;
                break;
            case JobType.Long:
                job.distance = Random.Range(9, 41) * 100;
                job.steps = job.distance * 3;
                job.timeInMinutes = Random.Range(3, 12) * 180;
                break;
        }

        job.fuelCost = job.distance * 10 * Random.Range(10, 15);
        job.experience = job.distance * 10;
        job.reward = job.distance * Random.Range(10, 15) / 3;
        var fleet = FleetManager.instance;
        job.reward = (long)(job.reward * upgradeManager.Get(UpgradeType.IncomeMultiplier) * fleet.RewardMultiplier);
        job.fuelCost = (long)(job.fuelCost / upgradeManager.Get(UpgradeType.FuelEfficiency) * fleet.FuelMultiplier);
        uiManager.AddJob(job);
    }

    public void AcceptJob(JobData job)
    {
        activeJob = new ActiveJobSaveData(job);
        Debug.Log(activeJob);
        JobSaveManager.SaveJob(activeJob);
        DisplayActiveJob();
        uiManager.ForceTab(TabType.ActiveJobs);
    }

    private void DisplayActiveJob()
    {
        uiManager.DisplayActiveJob(activeJob);
    }

    public void EndJob(bool isSuccess)
    {
        if (isSuccess)
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

    /// <summary>
    /// Single place walked steps are spent, for both live and offline steps:
    /// the active job first (each walked step also pulls BurnRate-1 steps of progress from the bank while it lasts,
    /// paying the burn loss on top), then the step bank up to its cap. Whatever doesn't fit is reported as overflow.
    /// </summary>
    public StepAllocation AllocateSteps(long steps)
    {
        var result = new StepAllocation { total = Math.Max(0, steps) };
        long remaining = result.total;
        if (remaining == 0) return result;

        if (activeJob != null && activeJob.stepsLeft > 0)
        {
            long need = activeJob.stepsLeft;
            long bank = currencyManager.GetCurrencyAmount(CurrencyType.BankedStep);
            double extraPerStep = BurnRate - 1.0;             // progress pulled from the bank per walked step
            double costPerExtra = 1.0 + BurnLoss(BurnRate);   // bank spent per step of that progress

            long walked, extra;
            if (extraPerStep <= 0 || bank <= 0)
            {
                walked = Math.Min(remaining, need);
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
                walked = Math.Min(remaining, walkedToFinish);
                extra = Math.Min(need - walked, (long)Math.Floor(Math.Min(walked, boostable) * extraPerStep));
            }

            // The epsilon keeps float noise (0.1f = 0.1000000015) from rounding 2,200 up to 2,201.
            long cost = Math.Min(bank, (long)Math.Ceiling(extra * costPerExtra - 1e-4));

            activeJob.stepsLeft -= walked + extra;
            remaining -= walked;
            result.toJob = walked;
            result.bankBurned = extra;
            result.bankCost = cost;

            if (cost > 0) currencyManager.AddCurrency(CurrencyType.BankedStep, -cost);
            uiManager.UpdateActiveJobStatus();
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