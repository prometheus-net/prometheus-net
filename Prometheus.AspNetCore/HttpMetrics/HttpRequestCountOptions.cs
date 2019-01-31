namespace Prometheus.HttpMetrics
{
    public sealed class HttpRequestCountOptions : HttpMetricsOptionsBase
    {
        private const string DefaultName = "http_requests_total";
        private const string DefaultHelp = "Provides the count of HTTP requests from an ASP.NET application.";

        public Counter Counter { get; set; } =
            Metrics.CreateCounter(DefaultName, DefaultHelp, HttpRequestLabelNames.All);
    }
}