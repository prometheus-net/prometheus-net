namespace Prometheus;

public sealed class PrometheusHealthCheckPublisherOptions
{
    private const string DefaultName = "aspnetcore_healthcheck_status";
    private const string DefaultHelp = "ASP.NET Core health check status (0 == Unhealthy, 0.5 == Degraded, 1 == Healthy)";

    public Gauge? Gauge { get; set; }    

    public Gauge GetDefaultGauge()
    {
        return Metrics.CreateGauge(DefaultName, DefaultHelp, labelNames: new[] { "name" });
    }
}
