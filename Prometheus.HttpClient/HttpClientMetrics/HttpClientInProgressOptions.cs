namespace Prometheus.HttpClientMetrics
{
    public sealed class HttpClientInProgressOptions : HttpClientMetricsOptionsBase
    {
       
        public ICollector<IGauge>? Gauge { get; set; }
    }
}