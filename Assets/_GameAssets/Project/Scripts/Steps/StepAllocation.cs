using System;

/// <summary>Where a batch of walked steps ended up. Every step lands in exactly one of toJob / toBank / overflow.</summary>
public struct StepAllocation
{
    public long total;      // walked steps processed
    public long toJob;      // walked steps that moved the active job
    public long bankBurned; // banked steps spent alongside them (2x speed while the bank lasts) - bonus, not part of total
    public long toBank;     // walked steps deposited into the step bank
    public long overflow;   // walked steps lost because the bank was full

    public long JobProgress => toJob + bankBurned;

    public void Add(StepAllocation other)
    {
        total += other.total;
        toJob += other.toJob;
        bankBurned += other.bankBurned;
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
