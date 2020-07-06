using Microsoft.AspNetCore.Http;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Prometheus.HttpMetrics
{
    public sealed class HttpRequestDurationMiddleware : HttpRequestMiddlewareBase<ICollector<IHistogram>, IHistogram>
    {
        private readonly RequestDelegate _next;

        public HttpRequestDurationMiddleware(RequestDelegate next, HttpRequestDurationOptions? options)
            : base(options, options?.Histogram)
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

        protected override string[] DefaultLabels => HttpRequestLabelNames.All;

        protected override ICollector<IHistogram> CreateMetricInstance(string[] labelNames) => MetricFactory.CreateHistogram(
            "http_request_duration_seconds",
            "The duration of HTTP requests processed by an ASP.NET Core application.",
            new HistogramConfiguration
            {
                // 1 ms to 32K ms buckets
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 16),
                LabelNames = labelNames
            });
    }
}