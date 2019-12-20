namespace Prometheus.HttpMetrics
{
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

        internal static readonly string[] RouteSpecific =
        {
            Controller,
            Action
        };
    }
}