using System;

namespace Prometheus.HttpExporter.AspNetCore.HttpRequestDuration
{
    public class HttpRequestDurationOptions : HttpExporterOptionsBase
    {
        public Histogram Histogram { get; set; } = Metrics.CreateHistogram(DefaultName, DefaultHelp,
            Histogram.ExponentialBuckets(0.0001, 1.5, 36), HttpRequestLabelNames.All);

        private const string DefaultName = "aspnet_http_request_duration";

        private const string DefaultHelp =
            "Provides the duration in milliseconds of HTTP requests from an ASP.NET application.";
    }
}