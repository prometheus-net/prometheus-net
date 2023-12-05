namespace Prometheus.HttpClientMetrics;

public sealed class HttpClientExporterOptions
{
    public HttpClientInProgressOptions InProgress { get; set; } = new HttpClientInProgressOptions();
    public HttpClientRequestCountOptions RequestCount { get; set; } = new HttpClientRequestCountOptions();
    public HttpClientRequestDurationOptions RequestDuration { get; set; } = new HttpClientRequestDurationOptions();
    public HttpClientResponseDurationOptions ResponseDuration { get; set; } = new HttpClientResponseDurationOptions();
}