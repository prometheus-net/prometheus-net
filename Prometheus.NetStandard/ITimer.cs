using System;

namespace Prometheus
{
    /// <summary>
    /// A timer that can be used to observe a duration of elapsed time.
    /// 
    /// The observation is made either when ObserveDuration is called or when the instance is disposed of.
    /// </summary>
    public interface ITimer : IDisposable
    {
        /// <summary>
        /// Observes the duration (in seconds) and returns the observed value.
        /// </summary>
        TimeSpan ObserveDuration();
    }
}
