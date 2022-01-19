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
            options = options ?? new HttpMiddlewareExporterOptions();

            void ApplyConfiguration(IApplicationBuilder builder)
            {
                builder.UseMiddleware<CaptureRouteDataMiddleware>(options);

                if (options.InProgress.Enabled)
                    builder.UseMiddleware<HttpInProgressMiddleware>(options.InProgress);
                if (options.RequestCount.Enabled)
                    builder.UseMiddleware<HttpRequestCountMiddleware>(options.RequestCount);
                if (options.RequestDuration.Enabled)
                    builder.UseMiddleware<HttpRequestDurationMiddleware>(options.RequestDuration);
            }

            if (options.CaptureMetricsUrl)
                ApplyConfiguration(app);
            else
                app.UseWhen(context => context.Request.Path != "/metrics", ApplyConfiguration);

            return app;
        }
    }
}