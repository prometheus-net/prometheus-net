namespace Prometheus.HttpClientMetrics
{
    public sealed class HttpClientHandlerExporterOptions
    {
        public HttpClientInProgressOptions InProgress { get; set; } = new HttpClientInProgressOptions();
        public HttpClientRequestCountOptions RequestCount { get; set; } = new HttpClientRequestCountOptions();
        public HttpClientRequestDurationOptions RequestDuration { get; set; } = new HttpClientRequestDurationOptions();
    }
}