namespace Prometheus.HttpClientMetrics;

public sealed class HttpClientInProgressOptions : HttpClientMetricsOptionsBase
{
    /// <summary>
    /// Set this to use a custom metric instead of the default.
    /// </summary>
    public ICollector<IGauge>? Gauge { get; set; }
}