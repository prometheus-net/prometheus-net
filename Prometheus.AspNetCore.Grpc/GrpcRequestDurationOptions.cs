namespace Prometheus
{
    public sealed class GrpcRequestDurationOptions : GrpcMetricsOptionsBase
    {
        /// <summary>
        /// Set this to use a custom metric instead of the default.
        /// </summary>
        public ICollector<IHistogram>? Histogram { get; set; }
    }
}