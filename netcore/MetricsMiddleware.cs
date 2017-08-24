
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.IO;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

using Prometheus;
using Prometheus.Advanced;

using static System.FormattableString;

namespace Prometheus.Middleware
{
    public class MetricsMiddleware
    {
        private readonly RequestDelegate next;
        private readonly ILogger logger;

        public MetricsMiddleware(RequestDelegate next, ILogger logger)
        {
            this.next = next;
            this.logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Path.Value.EndsWith("/metrics"))
            {
                context.Response.StatusCode = 200;
                context.Response.OnStarting(SetHeaders, state: context);
                var acceptHeaders = context.Request.Headers["Accept"];
                await Apply(acceptHeaders, context.Response);
            }
            else {
                await next.Invoke(context);                
            }
        }

        private Task SetHeaders(object state)
        {
            HttpContext context = (HttpContext)state;
            return Task.CompletedTask;
        }

        private async Task Apply(IEnumerable<string> acceptHeaders, HttpResponse response) {
            var registry = DefaultCollectorRegistry.Instance;
            var contentType = ScrapeHandler.GetContentType(acceptHeaders);
            response.ContentType = contentType;

            string s;
            using (MemoryStream stream = new MemoryStream()) {
                ScrapeHandler.ProcessScrapeRequest(registry.CollectAll(), contentType, stream);
                s = Encoding.UTF8.GetString(stream.ToArray());
            }
            response.ContentLength = s.Length;
            await response.WriteAsync(s);                        
        }
    }

    public static class OwinExtensions
    {
        public static IApplicationBuilder UseMetrics(this IApplicationBuilder builder, ILogger<MetricsMiddleware> logger)
        {
            return builder.UseMiddleware<MetricsMiddleware>(logger);
        }
    }
}