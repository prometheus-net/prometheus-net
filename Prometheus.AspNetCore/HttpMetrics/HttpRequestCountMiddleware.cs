using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace Prometheus.HttpMetrics
{
    public sealed class HttpRequestCountMiddleware : HttpRequestMiddlewareBase<ICollector<ICounter>, ICounter>
    {
        private readonly RequestDelegate _next;

        protected override string[] AllowedLabelNames => HttpRequestLabelNames.All;

        public HttpRequestCountMiddleware(RequestDelegate next, ICollector<ICounter> counter)
            : base(counter)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
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
                // So we would not have the labels if we tried to create the child early on.
                // In ASP.NET Core 3, it is actually available after .UseRouting() but we don't need to rush.
                CreateChild(context).Inc();
            }

        }
    }
}