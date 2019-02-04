using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace Prometheus.HttpMetrics
{
    public sealed class HttpRequestCountMiddleware : HttpRequestMiddlewareBase<Counter>
    {
        private readonly RequestDelegate _next;
        private readonly Counter _requestCount;

        public HttpRequestCountMiddleware(RequestDelegate next, Counter counter)
            : base(counter)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));

            _requestCount = counter;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            finally
            {
                // GetLabelData() route data is only available *after* invoking the next request delegate.
                _requestCount
                    .WithLabels(GetLabelData(context))
                    .Inc();
            }

        }
    }
}