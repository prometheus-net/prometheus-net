namespace Prometheus.HttpMetrics
{
    public sealed class HttpInProgressOptions : HttpMetricsOptionsBase
    {
        private const string DefaultName = "http_requests_in_progress";
        private const string DefaultHelp = "The number of requests currently in progress in the ASP.NET Core pipeline. One series without controller/action label values counts all in-progress requests, with separate series existing for each controller-action pair.";

        public Gauge Gauge { get; set; } =
            Metrics.CreateGauge(DefaultName, DefaultHelp, HttpRequestLabelNames.PotentiallyAvailableBeforeExecutingFinalHandler);
    }
}