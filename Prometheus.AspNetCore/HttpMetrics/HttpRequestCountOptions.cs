namespace Prometheus.HttpMetrics
{
    public sealed class HttpRequestCountOptions : HttpMetricsOptionsBase
    {
        private const string DefaultName = "http_requests_received_total";
        private const string DefaultHelp = "Provides the count of HTTP requests that have been processed by the ASP.NET Core pipeline.";

        public Counter Counter { get; set; } =
            Metrics.CreateCounter(DefaultName, DefaultHelp, HttpRequestLabelNames.All);
    }
}