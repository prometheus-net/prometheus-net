using Microsoft.AspNetCore.Builder;
using Prometheus.Advanced;

namespace Prometheus
{
    public static class PrometheusMiddlewareExtensions
    {
        public static IApplicationBuilder UsePrometheusServer(this IApplicationBuilder builder, string url = "/metrics", ICollectorRegistry registry = null)
        {
            // If there is a URL to map, map it and re-enter without the URL.
            if (url != null)
                return builder.Map(url, builder2 => builder2.UsePrometheusServer(null, registry));

            return builder.UseMiddleware<PrometheusMiddleware>(new PrometheusMiddleware.Settings
            {
                Registry = registry
            });
        }
    }
}
