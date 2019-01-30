using System;
using System.Diagnostics;

namespace Prometheus
{
    public sealed class Timer : IDisposable
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

        /// <summary>
        /// Observes the duration since the timer was created.
        /// </summary>
        public void ObserveDuration()
        {
            _observeDurationAction.Invoke();
        }

        public void Dispose()
        {
            ObserveDuration();
        }
    }
}