using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;

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
            _enableOpenMetrics = settings.EnableOpenMetrics;
        }

        public sealed class Settings
        {
            public CollectorRegistry? Registry { get; set; }
            public bool EnableOpenMetrics { get; set; } = false;
        }

        private readonly CollectorRegistry _registry;
        private readonly bool _enableOpenMetrics;

        private (ExpositionFormat, string) Negotiate(string header)
        {
            foreach (var candidate in header.Split(',')
                         .Select(MediaTypeWithQualityHeaderValue.Parse)
                         .OrderByDescending(mt => mt.Quality.GetValueOrDefault(1)))
                if (candidate.MediaType == PrometheusConstants.TextFmtContentType)
                {
                    break;
                }
                else if (_enableOpenMetrics && candidate.MediaType == PrometheusConstants.OpenMetricsContentType)
                {
                    return (ExpositionFormat.OpenMetricsText, PrometheusConstants.ExporterOpenMetricsContentType);
                }

            return (ExpositionFormat.Text, PrometheusConstants.ExporterContentType);
        }
        
        public async Task Invoke(HttpContext context)
        {
            var response = context.Response;
            
            try
            {
                var (fmt, contentType) = Negotiate(context.Request.Headers.Accept.ToString());    
                // We first touch the response.Body only in the callback because touching
                // it means we can no longer send headers (the status code).
                var serializer = new TextSerializer(delegate
                {
                    response.ContentType = contentType;
                    response.StatusCode = StatusCodes.Status200OK;
                    return response.Body;
                }, fmt: fmt);

                await _registry.CollectAndSerializeAsync(serializer, context.RequestAborted);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // The scrape was cancalled by the client. This is fine. Just swallow the exception to not generate pointless spam.
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
