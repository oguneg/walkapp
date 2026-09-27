using System;

/// <summary>
/// Single source of "now" for gameplay timers (step sync, express deadlines).
/// The debug offset lets you fast-forward time from SRDebugger instead of waiting an hour for a deadline.
/// </summary>
public static class GameClock
{
    private static TimeSpan debugOffset = TimeSpan.Zero;

    public static DateTime UtcNow => DateTime.UtcNow + debugOffset;
    public static long UnixNow => ToUnix(UtcNow);
    public static TimeSpan DebugOffset => debugOffset;

    public static long ToUnix(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();

    public static DateTime FromUnix(long unixSeconds) => DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;

    public static void DebugAdvance(TimeSpan amount) => debugOffset += amount;
    public static void DebugReset() => debugOffset = TimeSpan.Zero;

    /// <summary>"1h 05m", "12m 30s", "45s" — for countdowns and "away for" labels.</summary>
    public static string FormatDuration(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d {span.Hours}h";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes:00}m";
        if (span.TotalMinutes >= 1) return $"{span.Minutes}m {span.Seconds:00}s";
        return $"{span.Seconds}s";
    }

    /// <summary>"42:17" or "1:02:05" — for big ticking timers.</summary>
    public static string FormatClock(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes:00}:{span.Seconds:00}";
    }
}
