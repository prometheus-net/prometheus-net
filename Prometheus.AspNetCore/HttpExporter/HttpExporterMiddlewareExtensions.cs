using System;
using Microsoft.AspNetCore.Builder;
using Prometheus.HttpExporter.InFlight;
using Prometheus.HttpExporter.MvcRequestCount;

namespace Prometheus.HttpExporter
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

            if (options.InFlight.Enabled) app.UseMiddleware<HttpInFlightMiddleware>(options.InFlight);
            if (options.RequestCount.Enabled) app.UseMiddleware<MvcRequestCountMiddleware>(options.RequestCount);
           
            return app;
        } 
    }
}