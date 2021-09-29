using System.Collections.Generic;

namespace Prometheus.HttpMetrics
{
    public abstract class HttpMetricsOptionsBase
    {
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Transforms the <see cref="HttpRequestLabelNames.Code"/> label value from it's raw value (e.g. 200, 404) into a compressed
        /// alternative (e.g. 2xx, 4xx). Setting this to true can be used to reduce the cardinality of metrics produced while still clearly communicating
        /// success and error conditions (client vs server error). Defaults to false.
        /// </summary>
        public bool ReduceStatusCodeCardinality { get; set; } = false;

        /// <summary>
        /// Additional route parameters to include beyond the defaults (controller/action).
        /// This may be useful if you have, for example, a "version" parameter for API versioning.
        /// </summary>
        /// <remarks>
        /// Metric labels are automatically added for these parameters, unless you provide your
        /// own metric instance in the options (in which case you must add the required labels).
        /// </remarks>
        public List<HttpRouteParameterMapping> AdditionalRouteParameters { get; set; } = new List<HttpRouteParameterMapping>();

        /// <summary>
        /// Allows you to override the registry used to create the default metric instance.
        /// Value is ignored if you specify a custom metric instance in the options.
        /// </summary>
        public CollectorRegistry? Registry { get; set; }
    }
}