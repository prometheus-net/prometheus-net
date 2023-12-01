using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;

namespace Prometheus;

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
        /// <summary>
        /// Where do we take the metrics from. By default, we will take them from the global singleton registry.
        /// </summary>
        public CollectorRegistry? Registry { get; set; }

        /// <summary>
        /// Whether we support the OpenMetrics exposition format. Required to publish exemplars. Defaults to enabled.
        /// Use of OpenMetrics also requires that the client negotiate the OpenMetrics format via the HTTP "Accept" request header.
        /// </summary>
        public bool EnableOpenMetrics { get; set; } = true;
    }

    private readonly CollectorRegistry _registry;
    private readonly bool _enableOpenMetrics;

    private readonly record struct ProtocolNegotiationResult(ExpositionFormat ExpositionFormat, string ContentType);

    private static IEnumerable<MediaTypeWithQualityHeaderValue> ExtractAcceptableMediaTypes(string acceptHeaderValue)
    {
        var candidates = acceptHeaderValue.Split(',');

        foreach (var candidate in candidates)
        {
            // It is conceivably possible that some value is invalid - we filter them out here and only return valid values.
            // A common case is a missing/empty "Accept" header, in which case we just get 1 candidate of empty string (which is invalid).
            if (MediaTypeWithQualityHeaderValue.TryParse(candidate, out var mediaType))
                yield return mediaType;
        }
    }

    private ProtocolNegotiationResult NegotiateComminucationProtocol(HttpRequest request)
    {
        var acceptHeaderValues = request.Headers.Accept.ToString();

        // We allow the "Accept" HTTP header to be overridden by the "accept" query string parameter.
        // This is mainly for development purposes (to make it easier to request OpenMetrics format via browser URL bar).
        if (request.Query.TryGetValue("accept", out var acceptValuesFromQuery))
            acceptHeaderValues = string.Join(",", acceptValuesFromQuery);

        foreach (var candidate in ExtractAcceptableMediaTypes(acceptHeaderValues)
                     .OrderByDescending(mt => mt.Quality.GetValueOrDefault(1)))
        {
            if (candidate.MediaType == PrometheusConstants.TextContentType)
            {
                // The first preference is the text format. Fall throgh to the default case.
                break;
            }
            else if (_enableOpenMetrics && candidate.MediaType == PrometheusConstants.OpenMetricsContentType)
            {
                return new ProtocolNegotiationResult(ExpositionFormat.OpenMetricsText, PrometheusConstants.OpenMetricsContentTypeWithVersionAndEncoding);
            }
        }

        return new ProtocolNegotiationResult(ExpositionFormat.PrometheusText, PrometheusConstants.TextContentTypeWithVersionAndEncoding);
    }

    public async Task Invoke(HttpContext context)
    {
        var response = context.Response;

        try
        {
            var negotiationResult = NegotiateComminucationProtocol(context.Request);

            Stream GetResponseBodyStream()
            {
                // We first touch the response.Body only in the callback here because touching it means we can no longer send headers (the status code).
                // The collection logic will delay calling this method until it is reasonably confident that nothing will go wrong will the collection.
                response.ContentType = negotiationResult.ContentType;
                response.StatusCode = StatusCodes.Status200OK;

                return response.Body;
            }

            var serializer = new TextSerializer(GetResponseBodyStream, negotiationResult.ExpositionFormat);

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
                using var writer = new StreamWriter(response.Body, PrometheusConstants.ExportEncoding, bufferSize: -1, leaveOpen: true);
                await writer.WriteAsync(ex.Message);
            }
        }
    }
}
