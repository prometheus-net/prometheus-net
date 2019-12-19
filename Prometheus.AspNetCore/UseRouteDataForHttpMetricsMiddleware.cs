using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Threading.Tasks;

namespace Prometheus
{
    /// <summary>
    /// Uses ASP.NET Core MVC routing data from a specific point in the ASP.NET Core pipeline.
    /// Without this, the routing data is used from the point where the HTTP metrics middleware is installed,
    /// after request processing, which might be too late if something changed the routing data during processing.
    /// </summary>
    /// <remarks>
    /// This is useful if some steps overwrite the route data and you wish to record the original data in metrics.
    /// 
    /// If route data is available before executing the inner handler, it is used for metrics.
    /// Otherwise, route data is captured after executing the inner handler and is used for metrics.
    /// 
    /// If route data is avaialble before executing the inner handler AND the configured request duration metric
    /// includes controller/action labels that can make use of this data, labelled instances of the metric are created
    /// in addition to the default instance of the metric that does not differentiate by controller/action.
    /// </remarks>
    public sealed class UseRouteDataForHttpMetricsMiddleware
    {
        public UseRouteDataForHttpMetricsMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        private readonly RequestDelegate _next;

        public async Task Invoke(HttpContext context)
        {
            try
            {
                // In ASP.NET Core 3 with endpoint routing, the routing step happens separately and we can observe
                // the route data before the controller action is executed, thereby allowing us to use this data in metrics.
                var routeDataBeforeInnerHandler = TryCaptureRouteData(context);

                IGauge? inProgressGauge = null;

                if (routeDataBeforeInnerHandler != null)
                {
                    // We got some route data! This means we can create the "in progress" metric with accurate data.
                    // This assumes the relevant feature exists (true only if the metric has the label names defined).
                    var inProgressFeature = context.Features.Get<IHttpInProgressMetricWithRouteLabelsFeature>();
                    inProgressGauge = inProgressFeature?.CreateGauge(routeDataBeforeInnerHandler.Values);
                }

                using (inProgressGauge?.TrackInProgress())
                {
                    await _next(context);
                }
            }
            finally
            {
                // In ASP.NET Core 2, we have to capture it after executing the inner handler because
                // the routing happens as part of the MVC middleware, which is a single black box for us.
                if (context.Features.Get<ICapturedRouteDataFeature>() == null)
                    TryCaptureRouteData(context);
            }
        }

        private static ICapturedRouteDataFeature? TryCaptureRouteData(HttpContext context)
        {
            var routeData = context.GetRouteData();

            if (routeData == null || routeData.Values.Count <= 0)
                return null;

            var capturedRouteData = new CapturedRouteDataFeature();

            foreach (var pair in routeData.Values)
                capturedRouteData.Values.Add(pair.Key, pair.Value);

            context.Features.Set<ICapturedRouteDataFeature>(capturedRouteData);
            return capturedRouteData;
        }
    }
}
