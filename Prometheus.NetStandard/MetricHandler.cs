using System;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus
{
    /// <summary>
    /// Base class for various metric server implementations that start an independent exporter in the background.
    /// The expoters may either be pull-based (exposing the Prometheus API) or push-based (actively pushing to PushGateway).
    /// </summary>
    public abstract class MetricHandler : IMetricServer, IDisposable
    {
        // The registry that contains the collectors to export metrics from.
        // Subclasses are expected to use this variable to obtain the correct registry.
        protected readonly CollectorRegistry _registry;

        // The token is cancelled when the handler is instructed to stop.
        private CancellationTokenSource? _cts = new CancellationTokenSource();

        // This is the task started for the purpose of exporting metrics.
        private Task? _task;

        protected MetricHandler(CollectorRegistry? registry = null)
        {
            _registry = registry ?? Metrics.DefaultRegistry;
        }

        public IMetricServer Start()
        {
            if (_task != null)
                throw new InvalidOperationException("The metric server has already been started.");

            if (_cts == null)
                throw new InvalidOperationException("The metric server has already been started and stopped. Create a new server if you want to start it again.");

            _task = StartServer(_cts.Token);
            return this;
        }

        public async Task StopAsync()
        {
            // Signal the CTS to give a hint to the server thread that it is time to close up shop.
            _cts?.Cancel();
            
            try
            {
                if (_task == null)
                    return; // Never started.

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

        public void Stop()
        {
            // This method mainly exists for API compatiblity with prometheus-net v1. But it works, so that's fine.
            StopAsync().GetAwaiter().GetResult();
        }

        void IDisposable.Dispose()
        {
            Stop();
        }

        protected abstract Task StartServer(CancellationToken cancel);
    }
}
