namespace Prometheus.HttpExporter.AspNetCore.HttpRequestCount
{
    public class HttpRequestCountOptions : HttpExporterOptionsBase
    {
        public Counter Counter { get; set; } =
            Metrics.CreateCounter(DefaultName, DefaultHelp, "code", "method", "action", "controller");
        
        private const string DefaultName = "aspnet_http_request_count";
        private const string DefaultHelp = "Provides the count of HTTP requests from an ASP.NET application.";
    }
}