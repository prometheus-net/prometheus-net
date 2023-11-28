using System.Diagnostics;

namespace Prometheus;

internal class PlatformCompatibilityHelpers
{
    // Reimplementation of Stopwatch.GetElapsedTime (only available on .NET 7 or newer).
    public static TimeSpan StopwatchGetElapsedTime(long start, long end)
        => new((long)((end - start) * ((double)10_000_000 / Stopwatch.Frequency)));
}
