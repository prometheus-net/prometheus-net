using System.Net;
using System.Web.Http;

namespace Prometheus;

public static class AspNetMetricServer
{
    private const string RouteNamePrefix = "Prometheus_";

    public sealed class Settings
    {
        public CollectorRegistry? Registry { get; set; }
    }

    /// <summary>
    /// Registers an anonymous instance of the controller to be published on the /metrics URL.
    /// </summary>
    public static void RegisterRoutes(HttpConfiguration configuration, Settings? settings = null) =>
        MapRoute(configuration, "Default", "metrics", settings);

    /// <summary>
    /// Registers an anonymous instance of the controller to be published on a given URL path (e.g. "custom/metrics").
    /// </summary>
    public static void RegisterRoutes(HttpConfiguration configuration, string path, Settings? settings = null) =>
        MapRoute(configuration, path, path, settings);

    private static void MapRoute(HttpConfiguration configuration, string routeName, string routeTemplate, Settings? settings)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        configuration.Routes.MapHttpRoute(
            name: $"{RouteNamePrefix}{routeName}",
            routeTemplate: routeTemplate,
            defaults: null,
            constraints: null,
            handler: new Handler(settings?.Registry ?? Metrics.DefaultRegistry));
    }

    private sealed class Handler : HttpMessageHandler
    {
        public Handler(CollectorRegistry registry)
        {
            _registry = registry;
        }

        private readonly CollectorRegistry _registry;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // The ASP.NET PushStreamContent does not have a way to easily handle exceptions that
            // occur before we write to the stream (when we can still return nice error headers).
            // Maybe in a future version this could be improved, as right now exception == aborted connection.

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new PushStreamContent(async (stream, content, context) =>
                {
                    try
                    {
                        await _registry.CollectAndExportAsTextAsync(stream, ExpositionFormat.PrometheusText, cancellationToken);
                    }
                    finally
                    {
                        stream.Close();
                    }
                }, PrometheusConstants.ExporterContentTypeValue)
            };

            return Task.FromResult(response);
        }
    }
}
