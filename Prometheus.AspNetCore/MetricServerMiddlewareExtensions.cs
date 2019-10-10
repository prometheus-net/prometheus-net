using Microsoft.AspNetCore.Builder;

namespace Prometheus
{
    public static class MetricServerMiddlewareExtensions
    {
        /// <summary>
        /// Starts a Prometheus metrics exporter. The default URL is /metrics, which is a Prometheus convention.
        /// Use static methods on the <see cref="Metrics"/> class to create your metrics.
        /// </summary>
        public static IApplicationBuilder UseMetricServer(this IApplicationBuilder builder, string? url = "/metrics", CollectorRegistry? registry = null)
        {
            // If there is a URL to map, map it and re-enter without the URL.
            if (url != null)
                return builder.Map(url, builder2 => builder2.UseMetricServer((string?)null, registry));

            return builder.UseMiddleware<MetricServerMiddleware>(new MetricServerMiddleware.Settings
            {
                Registry = registry
            });
        }


        /// <summary>
        /// Starts a Prometheus metrics exporter on a specific port.
        /// Use static methods on the <see cref="Metrics"/> class to create your metrics.
        /// </summary>
        public static IApplicationBuilder UseMetricServer(this IApplicationBuilder builder, PathAndPort pathAndPort, CollectorRegistry? registry = null)
        {
            return builder.MapWhen(
                c => c.Connection.LocalPort == pathAndPort.Port,
                b0 => b0.Map(pathAndPort.Path, b1 => b1.UseMetricServer((string?)null, registry)));
        }

        public class PathAndPort
        {
            public PathAndPort(string path, int port)
            {
                Path = path;
                Port = port;
            }

            public static PathAndPort WithDefaultPath(int port)
            {
                return new PathAndPort("/metrics", port);
            }

            public string Path { get; set; }
            public int Port { get; set; }
        }
    }
}
