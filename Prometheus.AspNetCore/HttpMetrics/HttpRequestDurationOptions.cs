namespace Prometheus.HttpMetrics
{
    public sealed class HttpRequestDurationOptions : HttpMetricsOptionsBase
    {
        private const string DefaultName = "http_request_duration_seconds";

        private const string DefaultHelp =
            "Provides the duration in seconds of HTTP requests from an ASP.NET application.";

        public Histogram Histogram { get; set; } = Metrics.CreateHistogram(DefaultName, DefaultHelp,
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.0001, 1.5, 36),
                LabelNames = HttpRequestLabelNames.All
            });
    }
}