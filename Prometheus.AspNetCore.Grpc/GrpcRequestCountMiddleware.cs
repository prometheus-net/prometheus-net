using Microsoft.AspNetCore.Http;

namespace Prometheus;

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
        CreateChild(context)?.Inc();

        await _next(context);
    }

    protected override string[] DefaultLabels => GrpcRequestLabelNames.All;

    protected override ICollector<ICounter> CreateMetricInstance(string[] labelNames) => MetricFactory.CreateCounter(
        "grpc_requests_received_total",
        "Number of gRPC requests received (including those currently being processed).",
        labelNames);
}