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
        job.reward = (long)(job.reward * upgradeManager.globalMultipliers[(int)UpgradeType.IncomeMultiplier]);
        job.fuelCost = (long)(job.fuelCost / upgradeManager.globalMultipliers[(int)UpgradeType.FuelEfficiency]);
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
            currencyManager.AddCurrency(CurrencyType.Coin, activeJob.jobData.reward);
            uiManager.UpdateCompletedJobCount(completedJobCount);
        }

        activeJob = null;
        JobSaveManager.ClearJob();
    }

    public void RegisterSteps(int amount) => AllocateSteps(amount);

    /// <summary>
    /// Single place walked steps are spent, for both live and offline steps:
    /// the active job first (each walked step also burns one banked step while the bank lasts),
    /// then the step bank up to its cap. Whatever doesn't fit is reported as overflow.
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

            // Walked steps needed to finish: half the job while the bank can match every step, otherwise need - bank.
            long walkedToFinish = 2 * bank >= need ? (need + 1) / 2 : need - bank;
            long walked = Math.Min(remaining, walkedToFinish);
            long burned = Math.Min(Math.Min(walked, bank), need - walked);

            activeJob.stepsLeft -= walked + burned;
            remaining -= walked;
            result.toJob = walked;
            result.bankBurned = burned;

            if (burned > 0) currencyManager.AddCurrency(CurrencyType.BankedStep, -burned);
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