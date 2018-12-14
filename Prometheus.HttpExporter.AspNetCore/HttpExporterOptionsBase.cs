namespace Prometheus.HttpExporter.AspNetCore
{
    public class HttpExporterOptionsBase
    {
        public bool Enabled { get; set; }
        
        public string MetricName { get; set; }
        
        public string MetricDescription { get; set; } 
    }
}