using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace Prometheus.HttpMetrics
{
    public abstract class HttpMetricsOptionsBase
    {
        public bool Enabled { get; set; } = true;

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
        /// Allows you to add custom labels to http metrics.
        /// </summary>
        public IDictionary<string, Func<HttpContext, string>> AdditionalLabels { get; set; } =
            new Dictionary<string, Func<HttpContext, string>>();

        /// <summary>
        /// Allows you to override the registry used to create the default metric instance.
        /// Value is ignored if you specify a custom metric instance in the options.
        /// </summary>
        public CollectorRegistry? Registry { get; set; }
    }
}