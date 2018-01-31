using Microsoft.AspNetCore.Builder;
using Prometheus.Advanced;

namespace Prometheus
{
    public static class MetricServerMiddlewareExtensions
    {
        public static IApplicationBuilder UseMetricServer(this IApplicationBuilder builder, string url = "/metrics", ICollectorRegistry registry = null)
        {
            // If there is a URL to map, map it and re-enter without the URL.
            if (url != null)
                return builder.Map(url, builder2 => builder2.UseMetricServer(null, registry));

            return builder.UseMiddleware<MetricServerMiddleware>(new MetricServerMiddleware.Settings
            {
                Registry = registry
            });
        }
    }
}
