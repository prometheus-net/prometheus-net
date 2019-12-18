using Microsoft.AspNetCore.Routing;

namespace Prometheus
{
    sealed class CapturedRouteDataFeature : ICapturedRouteDataFeature
    {
        public RouteValueDictionary Values { get; } = new RouteValueDictionary();
    }
}
