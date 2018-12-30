using System;
using Microsoft.AspNetCore.Builder;
using Prometheus.HttpExporter.AspNetCore.HttpRequestCount;
using Prometheus.HttpExporter.AspNetCore.HttpRequestDuration;
using Prometheus.HttpExporter.AspNetCore.InFlight;

namespace Prometheus.HttpExporter.AspNetCore
{
    public static class HttpExporterMiddlewareExtensions
    {
        public static IApplicationBuilder UseHttpExporter(this IApplicationBuilder app,
            Action<HttpMiddlewareExporterOptions> configure)
        {
            var options = new HttpMiddlewareExporterOptions();

            configure?.Invoke(options);

            app.UseHttpExporter(options);
            
            return app;
        }
        
        public static IApplicationBuilder UseHttpExporter(this IApplicationBuilder app, HttpMiddlewareExporterOptions options = null)
        {
            if (options == null) options = new HttpMiddlewareExporterOptions();

            if (options.InFlight.Enabled) app.UseMiddleware<HttpInFlightMiddleware>();
            if (options.RequestCount.Enabled) app.UseMiddleware<HttpRequestCountMiddleware>(options.RequestCount);
            if (options.RequestDuration.Enabled) app.UseMiddleware<HttpRequestDurationMiddleware>(options.RequestDuration);
           
            return app;
        } 
    }
}