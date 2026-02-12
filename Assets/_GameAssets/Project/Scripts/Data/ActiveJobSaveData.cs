using System;
using UnityEngine;

// 1. The Wrapper Class that holds EVERYTHING for one active job
[System.Serializable]
public class ActiveJobSaveData
{
    public JobData jobData;
    public JobState state = JobState.None;
    public long acceptTimestamp;
    public long deadlineTimestamp;
    public long stepCountAtStart;
    public long targetStepCount;
    
    public bool isValid = false; 

    // Constructor to easily create a new active job
    public ActiveJobSaveData(JobData data)
    {
        this.jobData = data;
        this.stepCountAtStart = StepDisplayManager.instance.currentTotalSteps;
        this.targetStepCount = stepCountAtStart + data.steps;
        
        // Save current time as Ticks (a giant integer)
        this.acceptTimestamp = DateTime.Now.Ticks;
        
        // Calculate Deadline
        this.deadlineTimestamp = DateTime.Now.AddMinutes(data.timeInMinutes).Ticks;
        
        this.isValid = true;
    }
    
    public ActiveJobSaveData() { }

    public DateTime AcceptTime => new DateTime(acceptTimestamp);
    public DateTime DeadlineTime => new DateTime(deadlineTimestamp);
    
    public double TimeRemainingSeconds => (DeadlineTime - DateTime.Now).TotalSeconds;
}

public static class JobSaveManager
{
    private const string JOB_KEY = "CurrentActiveJob";

    public static void SaveJob(ActiveJobSaveData activeJob)
    {
        string json = JsonUtility.ToJson(activeJob);
        
        PlayerPrefs.SetString(JOB_KEY, json);
        PlayerPrefs.Save();
        
        Debug.Log("Job Saved: " + json);
    }
    
    public static ActiveJobSaveData LoadJob()
    {
        if (!PlayerPrefs.HasKey(JOB_KEY)) return null;

        string json = PlayerPrefs.GetString(JOB_KEY);

        try 
        {
            return JsonUtility.FromJson<ActiveJobSaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to load job: " + e.Message);
            return null;
        }
    }

    public static void ClearJob()
    {
        PlayerPrefs.DeleteKey(JOB_KEY);
        PlayerPrefs.Save();
    }
    
}