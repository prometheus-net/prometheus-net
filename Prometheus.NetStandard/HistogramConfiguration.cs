namespace Prometheus
{
    public sealed class HistogramConfiguration : MetricConfiguration
    {
        internal static readonly HistogramConfiguration Default = new HistogramConfiguration();

        public double[] Buckets { get; set; }
    }
}
