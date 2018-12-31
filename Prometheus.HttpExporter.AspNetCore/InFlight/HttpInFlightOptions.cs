namespace Prometheus.HttpExporter.AspNetCore.InFlight
{
    public class HttpInFlightOptions : HttpExporterOptionsBase
    {
        public Gauge Gauge { get; set; } =
            Metrics.CreateGauge(DefaultName, DefaultHelp);
        
        private const string DefaultName = "aspnet_http_inflight";
        private const string DefaultHelp = "Total number of requests currently being processed.";
    }
}