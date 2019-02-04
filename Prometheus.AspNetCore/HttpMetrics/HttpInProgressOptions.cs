namespace Prometheus.HttpMetrics
{
    public sealed class HttpInProgressOptions : HttpMetricsOptionsBase
    {
        private const string DefaultName = "http_requests_in_progress";
        private const string DefaultHelp = "The number of requests currently in progress in the ASP.NET Core pipeline.";

        public Gauge Gauge { get; set; } =
            Metrics.CreateGauge(DefaultName, DefaultHelp);
    }
}