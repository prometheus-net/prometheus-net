using System;
using System.Diagnostics;

namespace Prometheus
{
    public class Timer : IDisposable
    {
        private readonly Stopwatch _stopwatch;
        private readonly Action _reportDurationAction;

        public Timer(IObserver observer)
        {
            _reportDurationAction = () => observer.Observe(_stopwatch.Elapsed.TotalSeconds);
            _stopwatch = Stopwatch.StartNew();
        }

        public Timer(IGauge gauge)
        {
            _reportDurationAction = () => gauge.Set(_stopwatch.Elapsed.TotalSeconds);
            _stopwatch = Stopwatch.StartNew();
        }

        /// <summary>
        /// Records the duration since the timer was created.
        /// </summary>
        public void ApplyDuration()
        {
            _reportDurationAction.Invoke();
        }

        public void Dispose()
        {
            ApplyDuration();
        }
    }
}