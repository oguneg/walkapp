public static class NumberFormat
{
    /// <summary>9,999 -> "9,999", 57,160 -> "57.1K", 2,400,000 -> "2.4M". For tight HUD slots.</summary>
    public static string Compact(long value)
    {
        long abs = value < 0 ? -value : value;
        if (abs < 10_000) return value.ToString("N0");
        if (abs < 1_000_000) return Trim(value / 1_000d) + "K";
        if (abs < 1_000_000_000) return Trim(value / 1_000_000d) + "M";
        return Trim(value / 1_000_000_000d) + "B";
    }

    // One decimal below 100 (57.1K), none above (571K). Truncates so we never show more than the player has.
    private static string Trim(double v)
    {
        double abs = v < 0 ? -v : v;
        return abs < 100
            ? (System.Math.Truncate(v * 10) / 10).ToString("0.#")
            : System.Math.Truncate(v).ToString("0");
    }
}
