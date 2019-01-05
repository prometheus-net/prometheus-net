using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Prometheus.HttpExporter.AspNetCore.HttpRequestCount
{
    public class HttpRequestCountMiddleware : HttpRequestMiddlewareBase<Counter>
    {
        public HttpRequestCountMiddleware(RequestDelegate next, Counter counter)
            : base(counter)            
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));

            _requestCount = counter;
        }

        public async Task Invoke(HttpContext context)
        {
            await _next(context);

            var labels = GetLabelData(context);

            if (labels != null)
            {
                _requestCount.WithLabels(labels).Inc();
            }
        }

        private readonly RequestDelegate _next;
        private readonly Counter _requestCount;
    }
}