namespace Prometheus.HttpExporter.AspNetCore.InFlight
{
    public class HttpInFlightOptions : HttpExporterOptionsBase
    {
        public HttpInFlightOptions()
        {
            this.MetricName = "aspnet_http_inflight";
            
            this.MetricDescription= "Total number of requests currently being processed.";
        }
    }
}