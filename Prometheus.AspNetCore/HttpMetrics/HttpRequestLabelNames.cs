namespace Prometheus.HttpMetrics
{
    /// <summary>
    /// Label names reserved for the use by the HTTP request metrics.
    /// </summary>
    public static class HttpRequestLabelNames
    {
        public const string Code = "code";
        public const string Method = "method";
        public const string Controller = "controller";
        public const string Action = "action";

        public static readonly string[] All =
        {
            Code,
            Method,
            Controller,
            Action
        };

        internal static readonly string[] PotentiallyAvailableBeforeExecutingFinalHandler =
        {
            // Always available, part of request.
            Method,
            // These two are available only in ASP.NET Core 3.
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