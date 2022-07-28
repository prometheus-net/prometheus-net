using Microsoft.AspNetCore.Routing;

namespace Prometheus.HttpMetrics
{
    interface ICapturedRouteDataFeature
    {
        RouteValueDictionary Values { get; }
    }
}
