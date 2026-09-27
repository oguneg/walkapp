using System;

/// <summary>Where a batch of walked steps ended up. Every step lands in exactly one of toJob / toBank / overflow.</summary>
public struct StepAllocation
{
    public long total;      // walked steps processed
    public long toJob;      // walked steps that moved the active job
    public long bankBurned; // job progress pulled from the step bank (burn rate) - bonus, not part of total
    public long bankCost;   // banked steps that progress cost (more than bankBurned above the loss-free burn rate)
    public long toBank;     // walked steps deposited into the step bank
    public long overflow;   // walked steps lost because the bank was full

    public long JobProgress => toJob + bankBurned;

    public void Add(StepAllocation other)
    {
        total += other.total;
        toJob += other.toJob;
        bankBurned += other.bankBurned;
        bankCost += other.bankCost;
        toBank += other.toBank;
        overflow += other.overflow;
    }
}

/// <summary>Everything the "while you were away" popup needs.</summary>
public readonly struct OfflineStepReport
{
    public readonly StepAllocation allocation;
    public readonly TimeSpan? awayFor;

    public OfflineStepReport(StepAllocation allocation, TimeSpan? awayFor)
    {
        this.allocation = allocation;
        this.awayFor = awayFor;
    }
}
