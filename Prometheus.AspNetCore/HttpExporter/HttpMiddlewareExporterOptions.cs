namespace Prometheus.AspNetCore.HttpExporter
{
    public class HttpMiddlewareExporterOptions
    {
        public HttpInFlightOptions InFlight { get; set; } = new HttpInFlightOptions();
        public HttpRequestCountOptions RequestCount { get; set; } = new HttpRequestCountOptions();
        public HttpRequestDurationOptions RequestDuration { get; set; } = new HttpRequestDurationOptions();
    }
}