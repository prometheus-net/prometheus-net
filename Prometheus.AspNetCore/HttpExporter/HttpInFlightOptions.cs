namespace Prometheus.AspNetCore.HttpExporter
{
    public class HttpInFlightOptions : HttpExporterOptionsBase
    {
        private const string DefaultName = "http_executing_requests";
        private const string DefaultHelp = "The number of requests currently being processed.";

        public Gauge Gauge { get; set; } =
            Metrics.CreateGauge(DefaultName, DefaultHelp);
    }
}