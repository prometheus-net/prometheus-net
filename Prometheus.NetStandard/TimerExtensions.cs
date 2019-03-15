using System;
using System.Diagnostics;

namespace Prometheus
{
    public static class TimerExtensions
    {
        private sealed class Timer : ITimer
        {
            private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
            private readonly Action<double> _observeDurationAction;

            public Timer(IObserver observer)
            {
                _observeDurationAction = duration => observer.Observe(duration);
            }

            public Timer(IGauge gauge)
            {
                _observeDurationAction = duration => gauge.Set(duration);
            }

            public Timer(ICounter counter)
            {
                _observeDurationAction = duration => counter.Inc(duration);
            }

            public TimeSpan ObserveDuration()
            {
                var duration = _stopwatch.Elapsed;
                _observeDurationAction.Invoke(duration.TotalSeconds);

                return duration;
            }

            public void Dispose()
            {
                ObserveDuration();
            }
        }

        /// <summary>
        /// Enables you to easily report elapsed seconds in the value of an observer.
        /// Dispose of the returned instance to report the elapsed duration.
        /// </summary>
        public static ITimer NewTimer(this IObserver observer)
        {
            return new Timer(observer);
        }

        /// <summary>
        /// Enables you to easily report elapsed seconds in the value of a gauge.
        /// Dispose of the returned instance to report the elapsed duration.
        /// </summary>
        public static ITimer NewTimer(this IGauge gauge)
        {
            return new Timer(gauge);
        }

        /// <summary>
        /// Enables you to easily report elapsed seconds in the value of a counter.
        /// The duration (in seconds) will be added to the value of the counter.
        /// Dispose of the returned instance to report the elapsed duration.
        /// </summary>
        public static ITimer NewTimer(this ICounter counter)
        {
            return new Timer(counter);
        }
    }
}
