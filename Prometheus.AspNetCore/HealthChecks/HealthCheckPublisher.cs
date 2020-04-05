using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus.HealthChecks
{
    internal sealed class HealthCheckPublisher : IHealthCheckPublisher
    {
        private const string HEALTH_CHECK_LABEL = "hc";

        private readonly Gauge _healthCheckGaugeMetric;

        public HealthCheckPublisher()
        {
            _healthCheckGaugeMetric = Metrics.CreateGauge("healthcheck",
                "AspNetCore.Diagnostics.HealthChecks # 0={Unhealthy}, 1={Degraded}, 2={Healthy}", new GaugeConfiguration
                {
                    LabelNames = new string[] { HEALTH_CHECK_LABEL }
                });
        }

        private async Task SendHealthReportMetrics(HealthReport report)
        {
            await Task.Run(() =>
            {
                foreach (var reportEntry in report.Entries)
                    _healthCheckGaugeMetric.Labels(reportEntry.Key).
                        Set((double)reportEntry.Value.Status);
            });

        }
        public async Task PublishAsync(HealthReport report, CancellationToken cancellationToken) => await SendHealthReportMetrics(report);

    }
}
