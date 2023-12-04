namespace Prometheus.HttpMetrics;

public sealed class HttpRequestDurationOptions : HttpMetricsOptionsBase
{
    /// <summary>
    /// Set this to use a custom metric instead of the default.
    /// </summary>
    public ICollector<IHistogram>? Histogram { get; set; }
}