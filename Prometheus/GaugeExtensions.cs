namespace Prometheus
{
    public static class GaugeExtensions
    {
        /// <summary>
        /// Sets the value of the gauge to the current UTC time as a Unix timestamp in seconds.
        /// Value does not include any elapsed leap seconds because Unix timestamps do not include leap seconds.
        /// </summary>
        public static void SetToCurrentTimeUtc(this IGauge gauge)
        {
            gauge.Set(TimestampHelpers.ToUnixTimeSecondsAsDouble(DateTimeOffset.UtcNow));
        }

        /// <summary>
        /// Sets the value of the gauge to a specific moment as the UTC timezone Unix timestamp in seconds.
        /// Value does not include any elapsed leap seconds because Unix timestamps do not include leap seconds.
        /// </summary>
        public static void SetToTimeUtc(this IGauge gauge, DateTimeOffset timestamp)
        {
            gauge.Set(TimestampHelpers.ToUnixTimeSecondsAsDouble(timestamp));
        }

        /// <summary>
        /// Increments the value of the gauge to the current UTC time as a Unix timestamp in seconds.
        /// Value does not include any elapsed leap seconds because Unix timestamps do not include leap seconds.
        /// Operation is ignored if the current value is already greater.
        /// </summary>
        public static void IncToCurrentTimeUtc(this IGauge gauge)
        {
            gauge.IncTo(TimestampHelpers.ToUnixTimeSecondsAsDouble(DateTimeOffset.UtcNow));
        }

        /// <summary>
        /// Increments the value of the gauge to a specific moment as the UTC Unix timestamp in seconds.
        /// Value does not include any elapsed leap seconds because Unix timestamps do not include leap seconds.
        /// Operation is ignored if the current value is already greater.
        /// </summary>
        public static void IncToTimeUtc(this IGauge gauge, DateTimeOffset timestamp)
        {
            gauge.IncTo(TimestampHelpers.ToUnixTimeSecondsAsDouble(timestamp));
        }

        private sealed class InProgressTracker : IDisposable
        {
            public InProgressTracker(IGauge gauge)
            {
                _gauge = gauge;
            }

            public void Dispose()
            {
                _gauge.Dec();
            }

            private readonly IGauge _gauge;
        }

        /// <summary>
        /// Tracks the number of in-progress operations taking place.
        /// 
        /// Calling this increments the gauge. Disposing of the returned instance decrements it again.
        /// </summary>
        /// <remarks>
        /// It is safe to track the sum of multiple concurrent in-progress operations with the same gauge.
        /// </remarks>
        public static IDisposable TrackInProgress(this IGauge gauge)
        {
            if (gauge == null)
                throw new ArgumentNullException(nameof(gauge));

            gauge.Inc();

            return new InProgressTracker(gauge);
        }
    }
}
