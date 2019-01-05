using Prometheus.HttpExporter.AspNetCore.HttpRequestCount;
using Prometheus.HttpExporter.AspNetCore.HttpRequestDuration;
using Prometheus.HttpExporter.AspNetCore.InFlight;

namespace Prometheus.HttpExporter.AspNetCore.Library
{
    public class HttpMiddlewareExporterOptions
    {
        public HttpInFlightOptions InFlight { get; set; } = new HttpInFlightOptions();
        public HttpRequestCountOptions RequestCount { get; set; } = new HttpRequestCountOptions();
        public HttpRequestDurationOptions RequestDuration { get; set; } = new HttpRequestDurationOptions();
    }
}