namespace Prometheus.HttpClientMetrics;

public sealed class HttpClientRequestDurationOptions : HttpClientMetricsOptionsBase
{
    /// <summary>
    /// Set this to use a custom metric instead of the default.
    /// </summary>
    public ICollector<IHistogram>? Histogram { get; set; }
}