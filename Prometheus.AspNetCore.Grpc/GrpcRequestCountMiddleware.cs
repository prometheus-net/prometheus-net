using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace Prometheus
{
    /// <summary>
    /// Counts the number of requests to gRPC services.
    /// </summary>
    internal sealed class GrpcRequestCountMiddleware : GrpcRequestMiddlewareBase<ICollector<ICounter>, ICounter>
    {
        private readonly RequestDelegate _next;

        public GrpcRequestCountMiddleware(RequestDelegate next, GrpcRequestCountOptions? options)
            : base(options, options?.Counter)
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
                CreateChild(context)?.Inc();
            }
        }

        protected override string[] DefaultLabels => GrpcRequestLabelNames.All;

        protected override ICollector<ICounter> CreateMetricInstance(string[] labelNames) => MetricFactory.CreateCounter(
            "grpc_requests_received_total",
            "Number of gRPC requests received (including those currently being processed).",
            new CounterConfiguration
            {
                LabelNames = labelNames
            });
    }
}