using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Threading.Tasks;

namespace Prometheus.HttpMetrics
{
    /// <summary>
    /// If routing data is available before executing the inner handler, this routing data is captured
    /// and can be used later by other middlewares that wish not to be affected by runtime changes to routing data.
    /// </summary>
    /// <remarks>
    /// This is intended to be executed after the .UseRouting() middleware that performs ASP.NET Core 3 endpoint routing.
    /// 
    /// The captured route data is stored in the context via ICapturedRouteDataFeature.
    /// </remarks>
    public sealed class CaptureRouteDataMiddleware
    {
        private readonly RequestDelegate _next;

        public CaptureRouteDataMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public Task Invoke(HttpContext context)
        {
            TryCaptureRouteData(context);

            return _next(context);
        }

        private static void TryCaptureRouteData(HttpContext context)
        {
            var routeData = context.GetRouteData();

            if (routeData == null || routeData.Values.Count <= 0)
                return;

            var capturedRouteData = new CapturedRouteDataFeature();

            foreach (var pair in routeData.Values)
                capturedRouteData.Values.Add(pair.Key, pair.Value);

            context.Features.Set<ICapturedRouteDataFeature>(capturedRouteData);
        }
    }
}
