using Microsoft.AspNetCore.Builder;

namespace Prometheus
{
    public static class CaptureRouteDataMiddlewareExtensions
    {
        public static IApplicationBuilder CaptureRouteDataForHttpMetrics(this IApplicationBuilder app)
        {
            return app.UseMiddleware<CaptureRouteDataMiddleware>();
        }
    }
}
