using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Prometheus.HttpExporter.AspNetCore.HttpRequestCount
{
    public class HttpRequestCountMiddleware
    {
        public HttpRequestCountMiddleware(RequestDelegate next, HttpRequestCountOptions options)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            this.requestCount = Metrics.CreateCounter(options.MetricName, options.MetricDescription, "method", "code",
                "controller", "action");
        }

        public async Task Invoke(HttpContext context)
        {
            await this.next(context);

            var routeData = context.GetRouteData();

            if (routeData != null)
            {
                var requestMethod = context.Request.Method;
                var statusCode = context.Response.StatusCode;
                var actionName = routeData.Values["Action"] as string;
                var controllerName = routeData.Values["Controller"] as string;

                this.requestCount.WithLabels(requestMethod, statusCode.ToString(), controllerName, actionName).Inc();
            }
        }

        private readonly RequestDelegate next;
        private readonly Counter requestCount;
    }
}