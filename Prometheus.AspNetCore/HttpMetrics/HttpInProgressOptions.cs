namespace Prometheus.HttpMetrics;

public sealed class HttpInProgressOptions : HttpMetricsOptionsBase
{
    /// <summary>
    /// Set this to use a custom metric instead of the default.
    /// </summary>
    public ICollector<IGauge>? Gauge { get; set; }
}