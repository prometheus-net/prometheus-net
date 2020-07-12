namespace Prometheus.HttpClientMetrics
{
    public sealed class HttpClientRequestCountOptions : HttpClientMetricsOptionsBase
    {
        public ICollector<ICounter>? Counter { get; set; }
    }
}