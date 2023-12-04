using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Prometheus;

public static class KestrelMetricServerExtensions
{
    public static IServiceCollection AddMetricServer(this IServiceCollection services, Action<KestrelMetricServerOptions> optionsCallback)
    {
        return services.AddHostedService(sp =>
        {
            var options = new KestrelMetricServerOptions();
            optionsCallback(options);
            return new MetricsExporterService(options);
        });
    }

    private sealed class MetricsExporterService : BackgroundService
    {
        public MetricsExporterService(KestrelMetricServerOptions options)
        {
            _options = options;
        }

        private readonly KestrelMetricServerOptions _options;

        protected override async Task ExecuteAsync(CancellationToken cancel)
        {
            using var metricServer = new KestrelMetricServer(_options);
            metricServer.Start();

            try
            {
                // Wait forever, until we are told to stop.
                await Task.Delay(-1, cancel);
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                // Time to stop.
            }
        }
    }

}
