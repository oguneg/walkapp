using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class JobManager : MonoSingleton<JobManager>
{
    public List<JobData> availableJobs;
    public ActiveJobSaveData activeJob;
    public int completedJobCount = 0;
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
        var job =  new JobData();
        job.cargoType = (CargoType)Random.Range(0, 8);
        job.jobType = (JobType)Random.Range(0, 3);
        switch (job.jobType)
        {
            case JobType.Short :
                job.distance = Random.Range(15, 51);
                job.steps = job.distance * 3;
                job.timeInMinutes = job.steps / 10;
                break;
            case JobType.Medium : 
                job.distance = Random.Range(15, 50) * 10;
                job.steps = job.distance * 3;
                job.timeInMinutes = Random.Range(2,7) * 30;
                break;
            case JobType.Long : 
                job.distance = Random.Range(9, 41) * 100;
                job.steps = job.distance * 3;
                job.timeInMinutes = Random.Range(3,12) * 180;
                break;
        }

        job.fuelCost = job.distance * 10 * Random.Range(10, 15);
        job.reward = job.distance * Random.Range(10,15) / 3;
        
        UIManager.instance.AddJob(job);
    }

    public void AcceptJob(JobData job)
    {
        activeJob = new ActiveJobSaveData(job);
        Debug.Log(activeJob);
        JobSaveManager.SaveJob(activeJob);
        DisplayActiveJob();
    }

    private void DisplayActiveJob()
    {
        UIManager.instance.DisplayActiveJob(activeJob);
    }
    
    public void EndJob(bool isSuccess)
    {
        if (isSuccess)
        {
            completedJobCount++;
            ExperienceManager.instance.AddExperience(activeJob.jobData.distance * 10);
            CurrencyManager.instance.AddCurrency(CurrencyType.Coin, activeJob.jobData.reward);
            UIManager.instance.UpdateCompletedJobCount(completedJobCount);
        }
        
        activeJob = null;
        JobSaveManager.ClearJob();
    }
}
