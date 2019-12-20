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
                // We need to record metrics after inner handler execution because routing data in
                // ASP.NET Core 2 is only available *after* executing the next request delegate.
                // So we would not have the right labels if we tried to create the child early on.
                CreateChild(context).Inc();
            }
        }
    }
}