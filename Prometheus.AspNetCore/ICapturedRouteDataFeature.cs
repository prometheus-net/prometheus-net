using Microsoft.AspNetCore.Routing;

namespace Prometheus
{
    interface ICapturedRouteDataFeature
    {
        RouteValueDictionary Values { get; }
    }
}
