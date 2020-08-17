namespace Prometheus;

/// <summary>
/// Reserved label names used in gRPC metrics.
/// </summary>
public static class GrpcRequestLabelNames
{
    public const string Service = "service";
    public const string Method = "method";
    public const string Status = "status";

    public static readonly string[] All =
    {
        Service,
        Method,
        Status,
    };

    public static readonly string[] NoStatusSpecific =
    {
        Service,
        Method,
    };
}

