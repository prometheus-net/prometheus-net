using Microsoft.AspNetCore.Routing;

namespace Prometheus.HttpMetrics
{
    sealed class CapturedRouteDataFeature : ICapturedRouteDataFeature
    {
        public RouteValueDictionary Values { get; } = new RouteValueDictionary();
    }
}
