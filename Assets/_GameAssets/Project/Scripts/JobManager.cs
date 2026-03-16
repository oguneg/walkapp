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

    public void RegisterOfflineSteps(int amount)
    {
        if (activeJob != null && activeJob.stepsLeft > 0)
        {
            var leftoverSteps = amount;
            var stepsLeft = activeJob.stepsLeft;
            var bankedSteps = currencyManager.GetCurrencyAmount(CurrencyType.BankedStep);
            int usedSteps = 0;
            int burnedSteps = 0;
/////////////////////////////////////////
            Debug.Log($"steps left {activeJob.stepsLeft}");
            while (activeJob.stepsLeft > 0)
            {
                activeJob.stepsLeft--;
                amount--;
                usedSteps++;
                //Debug.Log("using step");
                if (bankedSteps > 0)
                {
                    activeJob.stepsLeft--;
                    burnedSteps++;
                    bankedSteps--;
                }
                if (amount <= 0)
                {
                    break;
                }
            }
            ReportActiveSteps(usedSteps);
            currencyManager.AddCurrency(CurrencyType.BankedStep, -burnedSteps);
///////////////////////////////////////
            uiManager.UpdateActiveJobStatus();
            if (activeJob.stepsLeft <= 0)
            {
                ReportBankedSteps((int)leftoverSteps-usedSteps);
                RegisterBankedSteps(leftoverSteps-usedSteps);
            }
        }
        else
        {
            ReportBankedSteps(amount);
            RegisterBankedSteps(amount);
        }
    }

    public void RegisterSteps(int amount)
    {
        if (activeJob != null && activeJob.stepsLeft > 0)
        {
            activeJob.stepsLeft -= amount;
            var bankedSteps = currencyManager.GetCurrencyAmount(CurrencyType.BankedStep);
            var maxBurnAmount = Math.Min(bankedSteps, activeJob.stepsLeft);
            if (maxBurnAmount < 0) maxBurnAmount = 0;
            var bankedStepsToBurn = Math.Clamp(amount, 0, maxBurnAmount);

            activeJob.stepsLeft -= bankedStepsToBurn;
            currencyManager.AddCurrency(CurrencyType.BankedStep, -bankedStepsToBurn);
            uiManager.UpdateActiveJobStatus();
        }
        else
        {
            RegisterBankedSteps(amount);
        }
    }

    private void ReportBankedSteps(int amount)
    {
        var bankableAmount = currencyManager.GetCurrencyCap(CurrencyType.BankedStep) -
                             currencyManager.GetCurrencyAmount(CurrencyType.BankedStep);
        StepManager.instance.bankedOfflineSteps = Mathf.Clamp(amount, 0,(int)bankableAmount);
    }

    private void ReportActiveSteps(int amount)
    {
        Debug.Log($"active steps reporting in {amount}");
        StepManager.instance.usedOfflineSteps = amount;
    }

    private void RegisterBankedSteps(long amount)
    {
        currencyManager.AddCurrency(CurrencyType.BankedStep, amount);
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