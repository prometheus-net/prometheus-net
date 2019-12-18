using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Threading.Tasks;

namespace Prometheus
{
    /// <summary>
    /// Enables you to capture the route data for Prometheus at any step in the ASP.NET Core workflow.
    /// This is useful if some steps overwrite the route data and you wish to record the original data in metrics.
    /// The route data is captured *after* executing the inner handler, before returning to the upper layer.
    /// </summary>
    public sealed class CaptureRouteDataMiddleware
    {
        public CaptureRouteDataMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        private readonly RequestDelegate _next;

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            finally
            {
                // We have to capture it after executing the inner handler because at least in ASP.NET Core 2
                // the routing happens as part of the MVC middleware, which is a single black box for us.
                var routeData = context.GetRouteData();

                if (routeData != null)
                {
                    var capturedRouteData = new CapturedRouteDataFeature();

                    foreach (var pair in routeData.Values)
                        capturedRouteData.Values.Add(pair.Key, pair.Value);

                    context.Features.Set<ICapturedRouteDataFeature>(capturedRouteData);
                }
            }
        }
    }
}
