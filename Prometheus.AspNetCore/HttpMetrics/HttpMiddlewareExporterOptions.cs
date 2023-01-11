using Microsoft.AspNetCore.Http;

namespace Prometheus.HttpMetrics
{
    public sealed class HttpMiddlewareExporterOptions
    {
        public HttpInProgressOptions InProgress { get; set; } = new HttpInProgressOptions();
        public HttpRequestCountOptions RequestCount { get; set; } = new HttpRequestCountOptions();
        public HttpRequestDurationOptions RequestDuration { get; set; } = new HttpRequestDurationOptions();

        /// <summary>
        /// Whether to capture metrics for queries to the MetricsUrl endpoint. Defaults to false.
        /// </summary>
        public bool CaptureMetricsUrl { get; set; }

        /// <summary>
        /// The Url used for the CaptureMetricsUrl property.
        /// This defaults to /metrics where metrics are exported by default
        /// </summary>
        public string MetricsUrl { get; set; } = "/metrics";

        /// <summary>
        /// Configures all the different types of metrics to use reduced status code cardinality (using 2xx instead of 200, 201 etc).
        /// </summary>
        public void ReduceStatusCodeCardinality()
        {
            InProgress.ReduceStatusCodeCardinality = true;
            RequestCount.ReduceStatusCodeCardinality = true;
            RequestDuration.ReduceStatusCodeCardinality = true;
        }

        /// <summary>
        /// Adds an additional route parameter to all the HTTP metrics.
        /// 
        /// Helper method to avoid manually adding it to each one.
        /// </summary>
        public void AddRouteParameter(HttpRouteParameterMapping mapping)
        {
            InProgress.AdditionalRouteParameters.Add(mapping);
            RequestCount.AdditionalRouteParameters.Add(mapping);
            RequestDuration.AdditionalRouteParameters.Add(mapping);
        }

        /// <summary>
        /// Adds a custom label to all the HTTP metrics.
        /// 
        /// Helper method to avoid manually adding it to each one.
        /// </summary>
        public void AddCustomLabel(HttpCustomLabel mapping)
        {
            InProgress.CustomLabels.Add(mapping);
            RequestCount.CustomLabels.Add(mapping);
            RequestDuration.CustomLabels.Add(mapping);
        }

        /// <summary>
        /// Adds a custom label to all the HTTP metrics.
        /// 
        /// Helper method to avoid manually adding it to each one.
        /// </summary>
        public void AddCustomLabel(string labelName, Func<HttpContext, string> valueProvider)
        {
            var mapping = new HttpCustomLabel(labelName, valueProvider);

            InProgress.CustomLabels.Add(mapping);
            RequestCount.CustomLabels.Add(mapping);
            RequestDuration.CustomLabels.Add(mapping);
        }
    }
}