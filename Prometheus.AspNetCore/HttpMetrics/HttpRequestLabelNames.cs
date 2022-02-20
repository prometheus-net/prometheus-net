namespace Prometheus.HttpMetrics
{
    /// <summary>
    /// Label names used by the HTTP request handler metrics system.
    /// </summary>
    public static class HttpRequestLabelNames
    {
        public const string Code = "code";
        public const string Method = "method";
        public const string Controller = "controller";
        public const string Action = "action";

        // Not reserved for background-compatibility, as it used to be optional and user-supplied.
        // Conditionally, it may also be automatically added to metrics.
        public const string Page = "page";

        // Not reserved for background-compatibility, as it used to be optional and user-supplied.
        public const string Endpoint = "endpoint";

        // These are reserved and may only be used with the default logic.
        public static readonly string[] Default =
        {
            Code,
            Method,
            Controller,
            Action
        };

        internal static readonly string[] DefaultsAvailableBeforeExecutingFinalHandler =
        {
            Method,
            Controller,
            Action
        };

        // Labels that do not need routing information to be collected.
        internal static readonly string[] NonRouteSpecific =
        {
            Code,
            Method
        };
    }
}