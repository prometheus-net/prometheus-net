using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Prometheus;

/// <summary>
/// Publishes ASP.NET Core Health Check statuses as Prometheus metrics.
/// </summary>
internal sealed class PrometheusHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly Gauge _checkStatus;

    public PrometheusHealthCheckPublisher(PrometheusHealthCheckPublisherOptions? options)
    {
        _checkStatus = options?.Gauge ?? new PrometheusHealthCheckPublisherOptions().GetDefaultGauge();
    }

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        foreach (var reportEntry in report.Entries)
            _checkStatus.WithLabels(reportEntry.Key).Set(HealthStatusToMetricValue(reportEntry.Value.Status));

        return Task.CompletedTask;
    }

    private static double HealthStatusToMetricValue(HealthStatus status)
    {
        switch (status)
        {
            case HealthStatus.Unhealthy:
                return 0;
            case HealthStatus.Degraded:
                return 0.5;
            case HealthStatus.Healthy:
                return 1;
            default:
                throw new NotSupportedException($"Unexpected HealthStatus value: {status}");
        }
    }
}
