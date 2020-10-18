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

            builder.Services.AddScoped<HttpClientInProgressHandler>();
            builder.Services.AddScoped<HttpClientRequestCountHandler>();
            builder.Services.AddScoped<HttpClientRequestDurationHandler>();

            if (options.InProgress.Enabled)
            {
                builder.Services.AddScoped(o => options.InProgress);
                builder = builder.AddHttpMessageHandler<HttpClientInProgressHandler>();
            }

            if (options.RequestCount.Enabled)
            {
                builder.Services.AddScoped(o => options.RequestCount);
                builder = builder.AddHttpMessageHandler<HttpClientRequestCountHandler>();
            }

            if (options.RequestDuration.Enabled)
            {
                builder.Services.AddScoped(o => options.RequestDuration);
                builder = builder.AddHttpMessageHandler<HttpClientRequestDurationHandler>();
            }

            return builder;
        }
    }
}