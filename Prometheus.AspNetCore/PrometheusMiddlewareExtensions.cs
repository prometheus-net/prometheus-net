using Microsoft.AspNetCore.Builder;
using Prometheus.Advanced;
using System.Collections.Generic;

namespace Prometheus
{
    public static class PrometheusMiddlewareExtensions
    {
        public static IApplicationBuilder UsePrometheusServer(this IApplicationBuilder builder, string url = "/metrics", IEnumerable<IOnDemandCollector> onDemandCollectors = null, ICollectorRegistry registry = null)
        {
            // If there is a URL to map, map it and re-enter without the URL.
            if (url != null)
                return builder.Map(url, builder2 => builder2.UsePrometheusServer(null, onDemandCollectors, registry));

            return builder.UseMiddleware<PrometheusMiddleware>(new PrometheusMiddleware.Settings
            {
                OnDemandCollectors = onDemandCollectors,
                Registry = registry
            });
        }
    }
}
