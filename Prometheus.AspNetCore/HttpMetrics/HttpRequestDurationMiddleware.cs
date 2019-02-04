using Microsoft.AspNetCore.Http;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Prometheus.HttpMetrics
{
    public sealed class HttpRequestDurationMiddleware : HttpRequestMiddlewareBase<Histogram>
    {
        private readonly RequestDelegate _next;
        private readonly Histogram _requestDuration;

        public HttpRequestDurationMiddleware(RequestDelegate next, Histogram histogram)
            : base(histogram)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _requestDuration = histogram;
        }

        public async Task Invoke(HttpContext context)
        {
            var stopWatch = Stopwatch.StartNew();

            // We need to write this out in long form instead of using a timer because
            // GetLabelData() can only return values *after* executing the next request delegate.
            try
            {
                await _next(context);
            }
            finally
            {
                stopWatch.Stop();

                // GetLabelData() route data is only available *after* invoking the next request delegate.
                _requestDuration
                    .WithLabels(GetLabelData(context))
                    .Observe(stopWatch.Elapsed.TotalSeconds);
            }
        }
    }
}