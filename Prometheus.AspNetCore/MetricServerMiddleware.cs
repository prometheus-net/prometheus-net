using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;

namespace Prometheus
{
    /// <summary>
    /// Prometheus metrics export middleware for ASP.NET Core.
    /// 
    /// You should use IApplicationBuilder.UseMetricServer extension method instead of using this class directly.
    /// </summary>
    public sealed class MetricServerMiddleware
    {
        public MetricServerMiddleware(RequestDelegate next, Settings settings)
        {
            _registry = settings.Registry ?? Metrics.DefaultRegistry;
        }

        public sealed class Settings
        {
            public CollectorRegistry? Registry { get; set; }
        }

        private readonly CollectorRegistry _registry;

        public async Task Invoke(HttpContext context)
        {
            var response = context.Response;

            try
            {
                // We first touch the response.Body only in the callback because touching
                // it means we can no longer send headers (the status code).
                var serializer = new TextSerializer(delegate
                {
                    response.ContentType = PrometheusConstants.ExporterContentType;
                    response.StatusCode = StatusCodes.Status200OK;
                    return response.Body;
                });

                await _registry.CollectAndSerializeAsync(serializer, context.RequestAborted);
            }
            catch (ScrapeFailedException ex)
            {
                // This can only happen before any serialization occurs, in the pre-collect callbacks.
                // So it should still be safe to update the status code and write an error message.
                response.StatusCode = StatusCodes.Status503ServiceUnavailable;

                if (!string.IsNullOrWhiteSpace(ex.Message))
                {
                    using (var writer = new StreamWriter(response.Body, PrometheusConstants.ExportEncoding,
                        bufferSize: -1, leaveOpen: true))
                        await writer.WriteAsync(ex.Message);
                }
            }
        }
    }
}
