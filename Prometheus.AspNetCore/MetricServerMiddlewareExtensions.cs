using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Prometheus
{
    public static class MetricServerMiddlewareExtensions
    {

#if NETCOREAPP3_0

        private const string DefaultDisplayName = "Prometheus metrics";

        /// <summary>
        /// Starts a Prometheus metrics exporter using endpoint routing.
        /// The default URL is /metrics, which is a Prometheus convention.
        /// Use static methods on the <see cref="Metrics"/> class to create your metrics.
        /// </summary>
        public static IEndpointConventionBuilder MapMetrics(
            this IEndpointRouteBuilder endpoints,
            string pattern = "/metrics",
            CollectorRegistry? registry = null
        )
        {

            var pipeline = endpoints
                .CreateApplicationBuilder()
                .UseMiddleware<MetricServerMiddleware>(
                    new MetricServerMiddleware.Settings
                    {
                        Registry = registry
                    }
                )
                .Build();

            return endpoints
                .Map(pattern, pipeline)
                .WithDisplayName(DefaultDisplayName);

        }

#endif

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
