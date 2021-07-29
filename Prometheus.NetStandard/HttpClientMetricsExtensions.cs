using Microsoft.Extensions.DependencyInjection;
using Prometheus.HttpClientMetrics;
using System;

namespace Prometheus
{
    public static class HttpClientMetricsExtensions
    {
        /// <summary>
        /// Configures the HttpClient pipeline to collect Prometheus metrics.
        /// </summary>
        public static IHttpClientBuilder UseHttpClientMetrics(this IHttpClientBuilder builder, Action<HttpClientExporterOptions> configure)
        {
            var options = new HttpClientExporterOptions();

            configure?.Invoke(options);

            builder.UseHttpClientMetrics(options);

            return builder;
        }

        /// <summary>
        /// Configures the HttpClient pipeline to collect Prometheus metrics.
        /// </summary>
        public static IHttpClientBuilder UseHttpClientMetrics(this IHttpClientBuilder builder, HttpClientExporterOptions? options = null)
        {
            options ??= new HttpClientExporterOptions();

            var identity = new HttpClientIdentity(builder.Name);

            if (options.InProgress.Enabled)
            {
                builder = builder.AddHttpMessageHandler(x => new HttpClientInProgressHandler(options.InProgress, identity));
            }

            if (options.RequestCount.Enabled)
            {
                builder = builder.AddHttpMessageHandler(x => new HttpClientRequestCountHandler(options.RequestCount, identity));
            }

            if (options.RequestDuration.Enabled)
            {
                builder = builder.AddHttpMessageHandler(x => new HttpClientRequestDurationHandler(options.RequestDuration, identity));
            }

            if (options.ResponseDuration.Enabled)
            {
                builder = builder.AddHttpMessageHandler(x => new HttpClientResponseDurationHandler(options.ResponseDuration, identity));
            }

            return builder;
        }
    }
}