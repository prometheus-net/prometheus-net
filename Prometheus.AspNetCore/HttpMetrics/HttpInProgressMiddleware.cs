using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Threading.Tasks;

namespace Prometheus.HttpMetrics
{
    public sealed class HttpInProgressMiddleware : HttpRequestMiddlewareBase<ICollector<IGauge>, IGauge>
    {
        private readonly RequestDelegate _next;

        protected override string[] AllowedLabelNames => HttpRequestLabelNames.PotentiallyAvailableBeforeExecutingFinalHandler;

        public HttpInProgressMiddleware(RequestDelegate next, ICollector<IGauge> gauge)
            : base(gauge)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public async Task Invoke(HttpContext context)
        {
            // If the metric we are using has route-specific labels, we register the feature to record
            // route-specific in-progress metrics once the route has been determined.
            if (_labelsIncludeRouteData)
            {
                // Will be called later on by the middleware that runs once we determine route data.
                // The other middleware will also do the actual duration tracking with the route data included,
                // we just give it the metric instance so it knows where to put the data.
                IGauge CreateChildLater(RouteValueDictionary routeData)
                {
                    return CreateChild(context, routeData);
                }

                context.Features.Set<IHttpInProgressMetricWithRouteLabelsFeature>(new InProgressFeatureWithRouteLabels(CreateChildLater));
            }

            // This will lack the route data. That's fine. If we get route data later on,
            // the route-specific middleware will use the above feature to add the route-specific child.
            using (CreateChild(context).TrackInProgress())
            {
                await _next(context);
            }
        }

        private class InProgressFeatureWithRouteLabels : IHttpInProgressMetricWithRouteLabelsFeature
        {
            private readonly Func<RouteValueDictionary, IGauge> _createGauge;

            public InProgressFeatureWithRouteLabels(Func<RouteValueDictionary, IGauge> createGauge)
            {
                _createGauge = createGauge;
            }

            public IGauge CreateGauge(RouteValueDictionary routeValues) => _createGauge(routeValues);
        }
    }
}