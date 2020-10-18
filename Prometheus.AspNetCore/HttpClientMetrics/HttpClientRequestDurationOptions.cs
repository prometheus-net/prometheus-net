namespace Prometheus.HttpClientMetrics
{
    public sealed class HttpClientRequestDurationOptions : HttpClientMetricsOptionsBase
    {
        public ICollector<IHistogram>? Histogram { get; set; }
    }
}