using Microsoft.AspNetCore.Builder;

namespace Prometheus
{
    public static class UseRouteDataForHttpMetricsMiddlewareExtensions
    {
        public static IApplicationBuilder UseRouteDataForHttpMetrics(this IApplicationBuilder app)
        {
            return app.UseMiddleware<UseRouteDataForHttpMetricsMiddleware>();
        }
    }
}
