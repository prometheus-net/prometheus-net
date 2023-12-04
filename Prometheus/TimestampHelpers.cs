namespace Prometheus;

static class TimestampHelpers
{
    // Math copypasted from DateTimeOffset.cs in .NET Framework.

    // Number of days in a non-leap year
    private const int DaysPerYear = 365;
    // Number of days in 4 years
    private const int DaysPer4Years = DaysPerYear * 4 + 1;       // 1461
    // Number of days in 100 years
    private const int DaysPer100Years = DaysPer4Years * 25 - 1;  // 36524
    // Number of days in 400 years
    private const int DaysPer400Years = DaysPer100Years * 4 + 1; // 146097
    private const int DaysTo1970 = DaysPer400Years * 4 + DaysPer100Years * 3 + DaysPer4Years * 17 + DaysPerYear; // 719,162
    private const long UnixEpochTicks = TimeSpan.TicksPerDay * DaysTo1970; // 621,355,968,000,000,000
    private const long UnixEpochSeconds = UnixEpochTicks / TimeSpan.TicksPerSecond; // 62,135,596,800

    public static double ToUnixTimeSecondsAsDouble(DateTimeOffset timestamp)
    {
        // This gets us sub-millisecond precision, which is better than ToUnixTimeMilliseconds().
        var ticksSinceUnixEpoch = timestamp.ToUniversalTime().Ticks - UnixEpochSeconds * TimeSpan.TicksPerSecond;
        return ticksSinceUnixEpoch / (double)TimeSpan.TicksPerSecond;
    }
}
