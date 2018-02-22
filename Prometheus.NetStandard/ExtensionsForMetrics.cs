using System;

namespace Prometheus
{
    public static class ExtensionsForMetrics
    {
        /// <summary>
        /// Enables you to easily report elapsed seconds in the value of a gauge.
        /// You need to manually call .ApplyDuration() on the returned instance to update the value of the gauge.
        /// </summary>
        public static Gauge.Timer StartTimer(this IGauge gauge)
        {
            return new Gauge.Timer(gauge);
        }

        /// <summary>
        /// Sets the value of the gauge to the current Unix timestamp in seconds.
        /// </summary>
        public static void SetToCurrentTime(this IGauge gauge)
        {
            var unixTicks = DateTime.UtcNow.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

            // Convert to double first to ensure that we can report fractional seconds.
            gauge.Set((double)unixTicks / TimeSpan.TicksPerSecond);
        }
    }
}
