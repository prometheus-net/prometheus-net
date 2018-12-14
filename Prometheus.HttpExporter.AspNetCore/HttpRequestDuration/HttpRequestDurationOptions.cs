namespace Prometheus.HttpExporter.AspNetCore.HttpRequestDuration
{
    public class HttpRequestDurationOptions : HttpExporterOptionsBase
    {
        public HttpRequestDurationOptions()
        {
            this.MetricName = "aspnet_http_request_duration";
            
            this.MetricDescription = "Provides the duration in milliseconds of HTTP requests from an ASP.NET application.";
        }

        public double[] HistogramBuckets { get; set; } = null;
    }
}