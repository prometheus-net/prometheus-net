using Microsoft.AspNetCore.Builder;

namespace Prometheus
{
    public static class UseRouteDataForHttpMetricsMiddlewareExtensions
    {
        /// <summary>
        /// Uses ASP.NET Core MVC routing data from this specific point in the ASP.NET Core pipeline.
        /// Without this, the routing data is used from the point where the HTTP metrics middleware is installed,
        /// after request processing, which might be too late if something changed the routing data during processing.
        /// </summary>
        public static IApplicationBuilder UseRouteDataForHttpMetrics(this IApplicationBuilder app)
        {
            return app.UseMiddleware<UseRouteDataForHttpMetricsMiddleware>();
        }
    }
}
