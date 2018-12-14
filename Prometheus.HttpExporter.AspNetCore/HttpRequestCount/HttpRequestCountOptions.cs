namespace Prometheus.HttpExporter.AspNetCore.HttpRequestCount
{
    public class HttpRequestCountOptions : HttpExporterOptionsBase
    {
        public HttpRequestCountOptions()
        {
            this.MetricName = "aspnet_http_request_count";
            
            this.MetricDescription = "Provides the count of HTTP requests from an ASP.NET application.";
        }
    }
}