using System.Collections.Generic;

namespace Prometheus.HttpClientMetrics
{
    /// <summary>
    ///     Label names reserved for the use by the HttpClient metrics.
    /// </summary>
    public static class HttpClientRequestLabelNames
    {
        public const string Method = "method";
        public const string Host = "host";

        public static readonly string[] All =
        {
            Method,
            Host
        };

        public static IDictionary<string, string> AsParameterMap()
        {
            var map = new Dictionary<string, string>(All.Length);

            foreach (var item in All)
            {
                map[item] = item;
            }

            return map;
        }
    }
}