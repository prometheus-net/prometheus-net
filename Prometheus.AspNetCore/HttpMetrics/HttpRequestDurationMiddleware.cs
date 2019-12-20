using Microsoft.AspNetCore.Http;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Prometheus.HttpMetrics
{
    public sealed class HttpRequestDurationMiddleware : HttpRequestMiddlewareBase<ICollector<IHistogram>, IHistogram>
    {
        private readonly RequestDelegate _next;

        protected override string[] AllowedLabelNames => HttpRequestLabelNames.All;

        public HttpRequestDurationMiddleware(RequestDelegate next, ICollector<IHistogram> histogram)
            : base(histogram)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public async Task Invoke(HttpContext context)
        {
            var stopWatch = Stopwatch.StartNew();

            // We need to write this out in long form instead of using a timer because routing data in
            // ASP.NET Core 2 is only available *after* executing the next request delegate.
            // So we would not have the right labels if we tried to create the child early on.
            try
            {
                await _next(context);
            }
            finally
            {
                stopWatch.Stop();

                CreateChild(context).Observe(stopWatch.Elapsed.TotalSeconds);
            }
        }
    }
}