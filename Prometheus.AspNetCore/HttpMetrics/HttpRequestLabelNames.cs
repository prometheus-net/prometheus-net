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
#if NETCOREAPP3_1
        public const string RoutePattern = "routepattern";
#endif

        public static readonly string[] All =
        {
            Code,
            Method,
            Controller,
            Action,
#if NETCOREAPP3_1
            RoutePattern
#endif
        };

        internal static readonly string[] PotentiallyAvailableBeforeExecutingFinalHandler =
        {
            // Always available, part of request.
            Method,
            // These two are available only in ASP.NET Core 3.
            Controller,
            Action,
#if NETCOREAPP3_1
            RoutePattern
#endif
        };

        // Labels that do not need routing information to be collected.
        internal static readonly string[] NonRouteSpecific =
        {
            Code,
            Method
        };
    }
}