using System;
using System.Diagnostics;

namespace Prometheus
{
    public static class TimerExtensions
    {
        private sealed class Timer : IDisposable
        {
            private readonly Stopwatch _stopwatch;
            private readonly Action _observeDurationAction;

            public Timer(IObserver observer)
            {
                _observeDurationAction = () => observer.Observe(_stopwatch.Elapsed.TotalSeconds);
                _stopwatch = Stopwatch.StartNew();
            }

            public Timer(IGauge gauge)
            {
                _observeDurationAction = () => gauge.Set(_stopwatch.Elapsed.TotalSeconds);
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _observeDurationAction.Invoke();
            }
        }

        /// <summary>
        /// Enables you to easily report elapsed seconds in the value of an observer.
        /// Dispose of the returned instance to report the elapsed duration.
        /// </summary>
        public static IDisposable NewTimer(this IObserver observer)
        {
            return new Timer(observer);
        }

        /// <summary>
        /// Enables you to easily report elapsed seconds in the value of a gauge.
        /// Dispose of the returned instance to report the elapsed duration.
        /// </summary>
        public static IDisposable NewTimer(this IGauge gauge)
        {
            return new Timer(gauge);
        }
    }
}
