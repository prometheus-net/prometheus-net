using Microsoft.AspNetCore.Routing;

namespace Prometheus
{
    /// <summary>
    /// This feature is registered by the HTTP metrics middleware if it finds that the "in progress" metric
    /// can benefit from route labels (by default, yes). If the "use route data" middleware finds that it
    /// has access to the necessary route data (never on ASP.NET Core 2, possibly on ASP.NET Core 3) then it
    /// will use this feature to increment the metric instance with the correct label values.
    /// </summary>
    interface IHttpInProgressMetricWithRouteLabelsFeature
    {
        IGauge CreateGauge(RouteValueDictionary routeValues);
    }
}
