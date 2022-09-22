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
        /// Metric labels are automatically defined for these parameters, unless you provide your
        /// own metric instance in the options (in which case you must add the required labels).
        /// </remarks>
        public List<HttpRouteParameterMapping> AdditionalRouteParameters { get; set; } = new List<HttpRouteParameterMapping>();

        /// <summary>
        /// Additional custom labels to add to the metrics, with values extracted from the HttpContext of incoming requests.
        /// </summary>
        /// <remarks>
        /// Metric labels are automatically defined for these, unless you provide your
        /// own metric instance in the options (in which case you must add the required labels).
        /// </remarks>
        public List<HttpCustomLabel> CustomLabels { get; set; } = new List<HttpCustomLabel>();

        /// <summary>
        /// Allows you to override the registry used to create the default metric instance.
        /// Value is ignored if you specify a custom metric instance in the options.
        /// </summary>
        public CollectorRegistry? Registry { get; set; }

        /// <summary>
        /// If set, the "page" label will be considered one of the built-in default labels.
        /// This is only enabled if Razor Pages is detected at the middleware setup stage.
        /// 
        /// The value is ignored if a custom metric is provided (though the user may still add
        /// the "page" label themselves via AdditionalRouteParameters and it will work).
        /// </summary>
        internal bool IncludePageLabelInDefaultsInternal { get; set; }
    }
}