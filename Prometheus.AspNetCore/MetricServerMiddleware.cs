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
            _next = next;

            _registry = settings.Registry ?? Metrics.DefaultRegistry;
        }

        public sealed class Settings
        {
            public CollectorRegistry Registry { get; set; }
        }

        private readonly RequestDelegate _next;

        private readonly CollectorRegistry _registry;

        public async Task Invoke(HttpContext context)
        {
            // We just handle the root URL (/metrics or whatnot).
            if (!string.IsNullOrWhiteSpace(context.Request.Path.Value.Trim('/')))
            {
                await _next(context);
                return;
            }

            var request = context.Request;
            var response = context.Response;

            try
            {
                using (var serializer = new TextSerializer(delegate
                    {
                        response.ContentType = PrometheusConstants.ExporterContentType;
                        response.StatusCode = StatusCodes.Status200OK;
                        return response.Body;
                    }, leaveOpen: false))
                {
                    _registry.CollectAndSerialize(serializer);
                }
            }
            catch (ScrapeFailedException ex)
            {
                // This can only happen before any serialization occurs, in the pre-collect callbacks.
                // So it should still be safe to update the status code and write an error message.
                response.StatusCode = StatusCodes.Status503ServiceUnavailable;

                if (!string.IsNullOrWhiteSpace(ex.Message))
                {
                    using (var writer = new StreamWriter(response.Body))
                        await writer.WriteAsync(ex.Message);
                }
            }
        }
    }
}
