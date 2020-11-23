namespace Prometheus.HttpClientMetrics
{
    /// <summary>
    /// Label names reserved for the use by the HttpClient metrics.
    /// </summary>
    public static class HttpClientRequestLabelNames
    {
        public const string Method = "method";
        public const string Host = "host";
        public const string IsCrawler = "isCrawler";

        public static readonly string[] All =
        {
            Method,
            Host,
            IsCrawler
        };
    }
}