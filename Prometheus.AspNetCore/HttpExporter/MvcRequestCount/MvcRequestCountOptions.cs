namespace Prometheus.HttpExporter.MvcRequestCount
{
    public class MvcRequestCountOptions : HttpExporterOptionsBase
    {
        public MvcRequestCountOptions()
        {
            this.MetricName = "aspnet_mvc_request_duration";
            
            this.MetricDescription = "Provides the duration of MVC requests (i.e. those that go to a Controller/Action) from an ASP.NET application.";
        }
    }
}