using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Prometheus
{
    internal sealed class GrpcRequestDurationMiddleware: GrpcRequestMiddlewareBase<ICollector<IHistogram>, IHistogram>
    {
        private readonly RequestDelegate _next;

        protected override string[] DefaultLabels => GrpcRequestLabelNames.All;

        public GrpcRequestDurationMiddleware(RequestDelegate next, GrpcRequestDurationOptions? options) : base(options, options?.Histogram)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var stopWatch = ValueStopwatch.StartNew();

            // We need to write this out in long form instead of using a timer because routing data in
            // ASP.NET Core 2 is only available *after* executing the next request delegate.
            // So we would not have the right labels if we tried to create the child early on.
            try
            {
                await _next(context);
            }
            finally
            {
                CreateChild(context)?.Observe(stopWatch.GetElapsedTime().TotalSeconds);
            }
        }

        protected override ICollector<IHistogram> CreateMetricInstance(string[] labelNames)
        {
            return MetricFactory.CreateHistogram(
                "grpc_request_duration_seconds",
                "The duration of GRPC requests processed by an ASP.NET Core application.",
                new HistogramConfiguration
                {
                    // 1 ms to 32K ms buckets
                    Buckets = Histogram.ExponentialBuckets(0.001, 2, 16),
                    LabelNames = labelNames
                });
        }
    }
}
