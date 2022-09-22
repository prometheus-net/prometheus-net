using System;
using System.Threading.Tasks;

namespace Prometheus
{
    /// <summary>
    /// A metric server exposes a Prometheus metric exporter endpoint in the background,
    /// operating independently and serving metrics until it is instructed to stop.
    /// </summary>
    public interface IMetricServer : IDisposable
    {
        /// <summary>
        /// Starts serving metrics.
        /// 
        /// Returns the same instance that was called (for fluent-API-style chaining).
        /// </summary>
        IMetricServer Start();

        /// <summary>
        /// Instructs the metric server to stop and returns a task you can await for it to stop.
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Instructs the metric server to stop and waits for it to stop.
        /// </summary>
        void Stop();
    }
}
