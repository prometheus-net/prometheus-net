using Prometheus.HttpMetrics;

namespace Prometheus
{
    public sealed class GrpcRequestCountOptions : HttpMetricsOptionsBase
    {
        private const string DefaultName = "grpc_requests_received_total";
        private const string DefaultHelp = "Provides the count of gRPC requests that have been processed";

        public Counter Counter { get; set; } =
            Metrics.CreateCounter(DefaultName, DefaultHelp, GrpcRequestLabelNames.All);
    }
}
