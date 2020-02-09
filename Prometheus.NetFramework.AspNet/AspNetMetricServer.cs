using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace Prometheus
{
    public static class AspNetMetricServer
    {
        public sealed class Settings
        {
            public CollectorRegistry Registry { get; set; }
        }

        /// <summary>
        /// Registers an anonymous instance of the controller to be published on the /metrics URL.
        /// </summary>
        public static void RegisterRoutes(HttpConfiguration configuration, Settings settings = null)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            configuration.Routes.MapHttpRoute(
                name: "Prometheus_Default",
                routeTemplate: "metrics",
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
                    Content = new PushStreamContent((stream, content, context) =>
                    {
                        return Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream, default);
                    }, PrometheusConstants.ExporterContentTypeMinimal)
                };

                return Task.FromResult(response);
            }
        }
    }
}
