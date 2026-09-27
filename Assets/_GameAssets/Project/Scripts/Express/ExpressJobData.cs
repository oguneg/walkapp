using System;

/// <summary>A time-limited offer: walk targetSteps within durationMinutes of accepting. Must be accepted before expiresUnix.</summary>
[Serializable]
public class ExpressOffer
{
    public CargoType cargoType;
    public int targetSteps;
    public int durationMinutes;
    public long reward;
    public long experience;
    public long createdUnix;
    public long expiresUnix;
}

public enum ExpressStatus
{
    Active,    // clock is running
    Resolving, // deadline passed, asking the OS for the final step count
    Completed, // reached the target, reward waiting to be claimed
    Failed     // deadline passed short of the target
}

[Serializable]
public class ExpressJob
{
    public ExpressOffer offer;
    public long acceptUnix;
    public long deadlineUnix;
    public ExpressStatus status;

    // Steps credited live inside the window while the app was open. Exact.
    public long liveSteps;
    // Offline steps pro-rated into the window. Only trusted when the OS has no step history for us.
    public long offlineEstimate;
    // Last OS step history reading for [accept, min(now, deadline)], and liveSteps at the moment it was requested.
    public long historySteps = -1;
    public long liveAtHistory;
    // Best progress seen so far. Never goes down, so the bar never jumps backwards.
    public long bestProgress;
    public long finalSteps;

    public DateTime AcceptUtc => GameClock.FromUnix(acceptUnix);
    public DateTime DeadlineUtc => GameClock.FromUnix(deadlineUnix);
    public TimeSpan TimeLeft => DeadlineUtc - GameClock.UtcNow;
    public long StepsLeft => Math.Max(0, offer.targetSteps - bestProgress);
    public float Progress01 => offer.targetSteps <= 0 ? 1f : Math.Min(1f, bestProgress / (float)offer.targetSteps);
}

[Serializable]
public class ExpressSaveData
{
    public bool hasOffer;
    public ExpressOffer offer = new ExpressOffer();
    public bool offerSeen;
    public bool hasJob;
    public ExpressJob job = new ExpressJob();
    public long nextOfferUnix;
    public int completedCount;
    public int failedCount;
}
