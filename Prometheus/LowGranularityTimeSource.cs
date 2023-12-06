using System.Diagnostics;

namespace Prometheus;

/// <summary>
/// We occasionally need timestamps to attach to metrics metadata. In high-performance code, calling the standard get-timestamp functions can be a nontrivial cost.
/// This class does some caching to avoid calling the expensive timestamp functions too often, giving an accurate but slightly lower granularity clock as one might otherwise get.
/// </summary>
internal static class LowGranularityTimeSource
{
    [ThreadStatic]
    private static double LastUnixSeconds;

    [ThreadStatic]
    private static long LastStopwatchTimestamp;

    [ThreadStatic]
    private static int LastTickCount;

    public static double GetSecondsFromUnixEpoch()
    {
        UpdateIfRequired();

        return LastUnixSeconds;
    }

    public static long GetStopwatchTimestamp()
    {
        UpdateIfRequired();

        return LastStopwatchTimestamp;
    }

    private static void UpdateIfRequired()
    {
        var currentTickCount = Environment.TickCount;

        if (LastTickCount != currentTickCount)
        {
            LastUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            LastStopwatchTimestamp = Stopwatch.GetTimestamp();
            LastTickCount = currentTickCount;
        }
    }
}
