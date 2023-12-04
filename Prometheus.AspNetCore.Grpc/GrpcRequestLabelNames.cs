namespace Prometheus;

/// <summary>
/// Reserved label names used in gRPC metrics.
/// </summary>
public static class GrpcRequestLabelNames
{
    public const string Service = "service";
    public const string Method = "method";

    public static readonly string[] All =
    {
        Service,
        Method,
    };
}
