using System;
using System.Threading.Tasks;

namespace Prometheus
{
    public class Timer : IDisposable
    {
        private readonly System.Diagnostics.Stopwatch _stopwatch;
        private readonly Action _reportDurationAction;

        public Timer(IObserver observer)
        {
            _reportDurationAction = () => observer.Observe(_stopwatch.Elapsed.TotalSeconds);
            _stopwatch = System.Diagnostics.Stopwatch.StartNew();
        }
        
        public Timer(IGauge gauge)
        {
            _reportDurationAction = () => gauge.Set(_stopwatch.Elapsed.TotalSeconds);
            _stopwatch = System.Diagnostics.Stopwatch.StartNew();
        }

        /// <summary>
        /// Records the duration since the timer was created.
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
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