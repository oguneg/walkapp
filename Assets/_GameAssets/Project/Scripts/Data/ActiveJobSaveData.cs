using System;
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

    public bool isValid = false;

    public ActiveJobSaveData(JobData data)
    {
        jobData = data;
        stepsLeft = data.steps;
        acceptUnix = GameClock.UnixNow;
        deadlineUnix = acceptUnix + Math.Max(1, data.timeInMinutes) * 60L;
        state = JobState.Active;
        isValid = true;
    }

    public ActiveJobSaveData() { }

    public DateTime AcceptUtc => GameClock.FromUnix(acceptUnix);
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

public static class JobSaveManager
{
    private const string JOB_KEY = "CurrentActiveJob";

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
