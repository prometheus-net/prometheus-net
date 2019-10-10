using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Prometheus
{
    public static class MetricServerMiddlewareExtensions
    {
        /// <summary>
        /// Starts a Prometheus metrics exporter on a specific port.
        /// Use static methods on the <see cref="Metrics"/> class to create your metrics.
        /// </summary>
        public static IApplicationBuilder UseMetricServer(this IApplicationBuilder builder, PathAndPort pathAndPort, CollectorRegistry? registry = null)
        {
            return builder
                .Map(pathAndPort.Path, b => b.MapWhen(PortMatches(), b1 => b1.InternalUseMiddleware(registry)));

            Func<HttpContext, bool> PortMatches()
            {
                return c => c.Connection.LocalPort == pathAndPort.Port;
            }
        }

        /// <summary>
        /// Starts a Prometheus metrics exporter. The default URL is /metrics, which is a Prometheus convention.
        /// Use static methods on the <see cref="Metrics"/> class to create your metrics.
        /// </summary>
        public static IApplicationBuilder UseMetricServer(this IApplicationBuilder builder, string? url = "/metrics", CollectorRegistry? registry = null)
        {
            // If there is a URL to map, map it and re-enter without the URL.
            if (url != null)
                return builder.Map(url, b => b.InternalUseMiddleware(registry));
            else
                return builder.InternalUseMiddleware(registry);
        }

        private static IApplicationBuilder InternalUseMiddleware(this IApplicationBuilder builder, CollectorRegistry? registry = null)
        {
            return builder.UseMiddleware<MetricServerMiddleware>(new MetricServerMiddleware.Settings
            {
                Registry = registry
            });
        }
    }

    public class PathAndPort
    {
        public PathAndPort(string path, int port)
        {
            Path = path;
            Port = port;
        }

        /// <summary>
        /// Creates a <see cref="PathAndPort"/> with the default URL <c>/metrics</c>, which is a Prometheus convention.
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        public static PathAndPort WithDefaultPath(int port)
        {
            return new PathAndPort("/metrics", port);
        }

        public string Path { get; }
        public int Port { get; }
    }
}
