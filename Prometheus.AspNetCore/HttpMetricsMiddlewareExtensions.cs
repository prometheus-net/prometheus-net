using Microsoft.AspNetCore.Builder;
using Prometheus.HttpMetrics;
using System;

namespace Prometheus
{
    public static class HttpMetricsMiddlewareExtensions
    {
        /// <summary>
        /// Configures the ASP.NET Core request pipeline to collect Prometheus metrics on processed HTTP requests.
        /// 
        /// If using ASP.NET Core 3 or newer, call this after .UseRouting().
        /// </summary>
        public static IApplicationBuilder UseHttpMetrics(this IApplicationBuilder app,
            Action<HttpMiddlewareExporterOptions> configure)
        {
            var options = new HttpMiddlewareExporterOptions();

            configure?.Invoke(options);

            app.UseHttpMetrics(options);

            return app;
        }

        /// <summary>
        /// Configures the ASP.NET Core request pipeline to collect Prometheus metrics on processed HTTP requests.
        /// 
        /// If using ASP.NET Core 3 or newer, call this after .UseRouting().
        /// </summary>
        public static IApplicationBuilder UseHttpMetrics(this IApplicationBuilder app,
            HttpMiddlewareExporterOptions? options = null)
        {
            if (options == null) options = new HttpMiddlewareExporterOptions();

            app.UseMiddleware<CaptureRouteDataMiddleware>();

            if (options.InProgress.Enabled)
                app.UseMiddleware<HttpInProgressMiddleware>(options.InProgress.Gauge);
            if (options.RequestCount.Enabled)
                app.UseMiddleware<HttpRequestCountMiddleware>(options.RequestCount.Counter);
            if (options.RequestDuration.Enabled)
                app.UseMiddleware<HttpRequestDurationMiddleware>(options.RequestDuration.Histogram);

            return app;
        }
    }
}