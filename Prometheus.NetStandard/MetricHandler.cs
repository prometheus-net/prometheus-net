using Prometheus.Advanced;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus
{
    /// <summary>
    /// Base class for various metric server implementations that start an independent exporter in the background.
    /// The expoters may either be pull-based (exposing the Prometheus API) or push-based (actively pushing to PushGateway).
    /// </summary>
    public abstract class MetricHandler : IMetricServer
    {
        // The registry that contains the collectors to export metrics from.
        // Subclasses are expected to use this variable to obtain the correct registry.
        protected readonly ICollectorRegistry _registry;

        // The token is cancelled when the handler is instructed to stop.
        private CancellationTokenSource _cts = new CancellationTokenSource();

        // This is the task started for the purpose of exporting metrics.
        private Task _task;

        protected MetricHandler(
            IEnumerable<IOnDemandCollector> onDemandCollectors = null,
            ICollectorRegistry registry = null)
        {
            _registry = registry ?? DefaultCollectorRegistry.Instance;

            if (_registry == DefaultCollectorRegistry.Instance)
            {
                // Default to DotNetStatsCollector if none specified
                // For no collectors, pass an empty collection
                if (onDemandCollectors == null)
                    onDemandCollectors = new[] { new DotNetStatsCollector() };

                DefaultCollectorRegistry.Instance.RegisterOnDemandCollectors(onDemandCollectors);
            }
        }

        public void Start()
        {
            if (_task != null)
                throw new InvalidOperationException("The metric server has already been started.");

            _task = StartServer(_cts.Token);
        }

        public async Task StopAsync()
        {
            // Signal the CTS to give a hint to the server thread that it is time to close up shop.
            _cts?.Cancel();
            
            try
            {
                // This will re-throw any exception that was caught on the StartServerAsync thread.
                // Perhaps not ideal behavior but hey, if the implementation does not want this to happen
                // it should have caught it itself in the background processing thread.
                await _task;
            }
            catch (OperationCanceledException)
            {
                // We'll eat this one, though, since it can easily get thrown by whatever checks the CancellationToken.
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }

        protected abstract Task StartServer(CancellationToken cancel);
    }
}
