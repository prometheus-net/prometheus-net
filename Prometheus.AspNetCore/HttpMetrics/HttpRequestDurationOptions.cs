namespace Prometheus.HttpMetrics
{
    public sealed class HttpRequestDurationOptions : HttpMetricsOptionsBase
    {
        private const string DefaultName = "http_request_duration_seconds";

        private const string DefaultHelp =
            "The duration of HTTP requests processed by an ASP.NET Core application.";

        public Histogram Histogram { get; set; } = Metrics.CreateHistogram(DefaultName, DefaultHelp,
            new HistogramConfiguration
            {
                // 1 ms to 32K ms buckets
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 16),
                LabelNames = HttpRequestLabelNames.All
            });
    }
}