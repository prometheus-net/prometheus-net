namespace Prometheus.Grpc
{
    public sealed class GrpcMiddlewareExporterOptions
    {
        public GrpcRequestCountOptions RequestCount { get; set; } = new GrpcRequestCountOptions();
    }
}
