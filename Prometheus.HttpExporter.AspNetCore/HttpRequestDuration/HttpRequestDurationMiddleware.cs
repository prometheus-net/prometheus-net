using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Prometheus.HttpExporter.AspNetCore.HttpRequestDuration
{
    public class HttpRequestDurationMiddleware
    {
        public HttpRequestDurationMiddleware(RequestDelegate next, HttpRequestDurationOptions options)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            
            var histogramConfiguration = new HistogramConfiguration
            {
                Buckets = options.HistogramBuckets,
                LabelNames = new[] {"method", "code", "controller", "action"}
            };

            this.requestDuration =
                Metrics.CreateHistogram(options.MetricName, options.MetricDescription, histogramConfiguration);
        }

        public async Task Invoke(HttpContext context)
        {
            var stopWatch = new Stopwatch();
            await this.next(context);
            stopWatch.Stop();

            var routeData = context.GetRouteData();

            if (routeData != null)
            {
                var requestMethod = context.Request.Method;
                var statusCode = context.Response.StatusCode;
                var actionName = routeData.Values["Action"] as string;
                var controllerName = routeData.Values["Controller"] as string;

                this.requestDuration.WithLabels(requestMethod, statusCode.ToString(), controllerName, actionName)
                    .Observe(stopWatch.ElapsedMilliseconds);
            }
        }

        private readonly RequestDelegate next;
        private readonly Histogram requestDuration;
    }
}