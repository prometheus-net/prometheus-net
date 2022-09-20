using Prometheus;

namespace Sample.Web.DifferentPort;

/// <summary>
/// This class exists to wire up the metrics server on a different port.
/// </summary>
public sealed class MetricsExporterService : BackgroundService
{
    public const ushort MetricsPort = 1234;

    protected override async Task ExecuteAsync(CancellationToken cancel)
    {
        using var metricServer = new KestrelMetricServer(port: MetricsPort);
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
