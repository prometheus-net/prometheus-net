using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.ComponentModel;

namespace Prometheus;

public static class MetricServerMiddlewareExtensions
{
    private const string DefaultDisplayName = "Prometheus metrics";

    /// <summary>
    /// Starts a Prometheus metrics exporter using endpoint routing.
    /// The default URL is /metrics, which is a Prometheus convention.
    /// Use static methods on the <see cref="Metrics"/> class to create your metrics.
    /// </summary>
    public static IEndpointConventionBuilder MapMetrics(
        this IEndpointRouteBuilder endpoints,
        Action<MetricServerMiddleware.Settings> configure,
        string pattern = "/metrics"
    )
    {
        var pipeline = endpoints
            .CreateApplicationBuilder()
            .InternalUseMiddleware(configure)
            .Build();

        return endpoints
            .Map(pattern, pipeline)
            .WithDisplayName(DefaultDisplayName);
    }

    /// <summary>
    /// Starts a Prometheus metrics exporter, filtering to only handle requests received on a specific port.
    /// The default URL is /metrics, which is a Prometheus convention.
    /// Use static methods on the <see cref="Metrics"/> class to create your metrics.
    /// </summary>
    public static IApplicationBuilder UseMetricServer(
        this IApplicationBuilder builder,
        int port,
        Action<MetricServerMiddleware.Settings> configure,
        string? url = "/metrics")
    {
        // If no URL, use root URL.
        url ??= "/";

        return builder
            .Map(url, b => b.MapWhen(PortMatches(), b1 => b1.InternalUseMiddleware(configure)));

        Func<HttpContext, bool> PortMatches()
        {
            return c => c.Connection.LocalPort == port;
        }
    }

    /// <summary>
    /// Starts a Prometheus metrics exporter.
    /// The default URL is /metrics, which is a Prometheus convention.
    /// Use static methods on the <see cref="Metrics"/> class to create your metrics.
    /// </summary>
    public static IApplicationBuilder UseMetricServer(
        this IApplicationBuilder builder,
        Action<MetricServerMiddleware.Settings> configure,
        string? url = "/metrics")
    {
        if (url != null)
            return builder.Map(url, b => b.InternalUseMiddleware(configure));
        else
            return builder.InternalUseMiddleware(configure);
    }

    #region Legacy methods without the configure action
    /// <summary>
    /// Starts a Prometheus metrics exporter using endpoint routing.
    /// The default URL is /metrics, which is a Prometheus convention.
    /// Use static methods on the <see cref="Metrics"/> class to create your metrics.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)] // It is not exactly obsolete but let's de-emphasize it.
    public static IEndpointConventionBuilder MapMetrics(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/metrics",
        CollectorRegistry? registry = null
    )
    {
        return MapMetrics(endpoints, LegacyConfigure(registry), pattern);
    }

    /// <summary>
    /// Starts a Prometheus metrics exporter, filtering to only handle requests received on a specific port.
    /// The default URL is /metrics, which is a Prometheus convention.
    /// Use static methods on the <see cref="Metrics"/> class to create your metrics.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)] // It is not exactly obsolete but let's de-emphasize it.
    public static IApplicationBuilder UseMetricServer(
        this IApplicationBuilder builder,
        int port,
        string? url = "/metrics",
        CollectorRegistry? registry = null)
    {
        return UseMetricServer(builder, port, LegacyConfigure(registry), url);
    }

    /// <summary>
    /// Starts a Prometheus metrics exporter.
    /// The default URL is /metrics, which is a Prometheus convention.
    /// Use static methods on the <see cref="Metrics"/> class to create your metrics.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)] // It is not exactly obsolete but let's de-emphasize it.
    public static IApplicationBuilder UseMetricServer(
        this IApplicationBuilder builder,
        string? url = "/metrics",
        CollectorRegistry? registry = null)
    {
        return UseMetricServer(builder, LegacyConfigure(registry), url);
    }

    private static Action<MetricServerMiddleware.Settings> LegacyConfigure(CollectorRegistry? registry) =>
        (MetricServerMiddleware.Settings settings) =>
        {
            settings.Registry = registry;
        };
    #endregion

    private static IApplicationBuilder InternalUseMiddleware(this IApplicationBuilder builder, Action<MetricServerMiddleware.Settings> configure)
    {
        var settings = new MetricServerMiddleware.Settings();
        configure(settings);

        return builder.UseMiddleware<MetricServerMiddleware>(settings);
    }
}
