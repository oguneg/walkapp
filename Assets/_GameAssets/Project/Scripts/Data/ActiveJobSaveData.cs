using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything saved for the one regular job in progress.
/// Active until the steps are done (Claimable, no time limit on claiming) or the deadline passes first (Failed).
/// </summary>
[Serializable]
public class ActiveJobSaveData
{
    public JobData jobData;
    public JobState state = JobState.Active;
    public long acceptUnix;
    public long deadlineUnix;
    public long stepsLeft;
    // Walked steps credited to this job (bank burn not included). The deadline check compares it with the
    // OS step history for [accept, deadline] to catch steps that were walked in time but credited late.
    public long walkedSteps;
    // When the job started taking steps: its accept time, or later for a job that waited in the queue
    // (its deadline runs from when it was queued, its steps only from here).
    public long activeFromUnix;

    public bool isValid = false;

    public ActiveJobSaveData(JobData data)
    {
        jobData = data;
        stepsLeft = data.steps;
        acceptUnix = GameClock.UnixNow;
        deadlineUnix = acceptUnix + Math.Max(1, data.timeInMinutes) * 60L;
        activeFromUnix = acceptUnix;
        state = JobState.Active;
        isValid = true;
    }

    public ActiveJobSaveData() { }

    public DateTime AcceptUtc => GameClock.FromUnix(acceptUnix);
    public DateTime ActiveFromUtc => GameClock.FromUnix(activeFromUnix > 0 ? activeFromUnix : acceptUnix);
    public DateTime DeadlineUtc => GameClock.FromUnix(deadlineUnix);
    public TimeSpan TimeLeft => DeadlineUtc - GameClock.UtcNow;
    public bool IsRunning => state == JobState.Active;
    public bool IsPastDeadline => GameClock.UnixNow >= deadlineUnix;

    /// <summary>Share of the time limit already used, 0..1.</summary>
    public float TimeUsed01
    {
        get
        {
            long span = deadlineUnix - acceptUnix;
            return span <= 0 ? 1f : Mathf.Clamp01((GameClock.UnixNow - acceptUnix) / (float)span);
        }
    }
}

[Serializable]
public class FinishedJobList
{
    public List<ActiveJobSaveData> jobs = new List<ActiveJobSaveData>();
}

public static class JobSaveManager
{
    private const string FINISHED_KEY = "FinishedJobs";

    public static void SaveFinished(List<ActiveJobSaveData> jobs)
    {
        PlayerPrefs.SetString(FINISHED_KEY, JsonUtility.ToJson(new FinishedJobList { jobs = jobs }));
        PlayerPrefs.Save();
    }

    public static List<ActiveJobSaveData> LoadFinished()
    {
        if (!PlayerPrefs.HasKey(FINISHED_KEY)) return new List<ActiveJobSaveData>();
        try
        {
            var list = JsonUtility.FromJson<FinishedJobList>(PlayerPrefs.GetString(FINISHED_KEY));
            return list?.jobs?.FindAll(j => j != null && j.jobData != null) ?? new List<ActiveJobSaveData>();
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to load finished jobs: " + e.Message);
            return new List<ActiveJobSaveData>();
        }
    }

    private const string JOB_KEY = "CurrentActiveJob";
    private const string QUEUE_KEY = "QueuedJob";

    public static void SaveQueued(ActiveJobSaveData job)
    {
        PlayerPrefs.SetString(QUEUE_KEY, JsonUtility.ToJson(job));
        PlayerPrefs.Save();
    }

    public static ActiveJobSaveData LoadQueued()
    {
        if (!PlayerPrefs.HasKey(QUEUE_KEY)) return null;
        try
        {
            var job = JsonUtility.FromJson<ActiveJobSaveData>(PlayerPrefs.GetString(QUEUE_KEY));
            return job != null && job.jobData != null ? job : null;
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to load queued job: " + e.Message);
            return null;
        }
    }

    public static void ClearQueued()
    {
        PlayerPrefs.DeleteKey(QUEUE_KEY);
        PlayerPrefs.Save();
    }

    public static void SaveJob(ActiveJobSaveData activeJob)
    {
        string json = JsonUtility.ToJson(activeJob);

        PlayerPrefs.SetString(JOB_KEY, json);
        PlayerPrefs.Save();
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
