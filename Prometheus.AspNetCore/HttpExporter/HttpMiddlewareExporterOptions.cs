using Prometheus.HttpExporter.InFlight;
using Prometheus.HttpExporter.MvcRequestCount;

namespace Prometheus.HttpExporter
{
    public class HttpMiddlewareExporterOptions
    {
        public HttpInFlightOptions InFlight { get; set; } = new HttpInFlightOptions();
        public MvcRequestCountOptions RequestCount { get; set; } = new MvcRequestCountOptions();
    }
}