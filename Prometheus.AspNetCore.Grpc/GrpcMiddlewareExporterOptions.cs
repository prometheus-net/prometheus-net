namespace Prometheus;

public sealed class GrpcMiddlewareExporterOptions
{
    public GrpcRequestCountOptions RequestCount { get; set; } = new GrpcRequestCountOptions();
}
