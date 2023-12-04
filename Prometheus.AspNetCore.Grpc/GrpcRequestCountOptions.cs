namespace Prometheus;

public sealed class GrpcRequestCountOptions : GrpcMetricsOptionsBase
{
    /// <summary>
    /// Set this to use a custom metric instead of the default.
    /// </summary>
    public ICollector<ICounter>? Counter { get; set; }
}
