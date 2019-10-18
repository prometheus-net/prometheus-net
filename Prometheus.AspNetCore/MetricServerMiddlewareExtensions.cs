using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Prometheus
{
    public static class MetricServerMiddlewareExtensions
    {
        /// <summary>
        /// Starts a Prometheus metrics exporter, filtering to only handle requests received on a specific port.
        /// The default URL is /metrics, which is a Prometheus convention.
        /// Use static methods on the <see cref="Metrics"/> class to create your metrics.
        /// </summary>
        public static IApplicationBuilder UseMetricServer(this IApplicationBuilder builder, int port, string? url = "/metrics", CollectorRegistry? registry = null)
        {
            return builder
                .Map(url, b => b.MapWhen(PortMatches(), b1 => b1.InternalUseMiddleware(registry)));

            Func<HttpContext, bool> PortMatches()
            {
                return c => c.Connection.LocalPort == port;
            }
        }

        /// <summary>
        /// Starts a Prometheus metrics exporter.
        /// The default URL is /metrics, which is a Prometheus convention.
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
}
