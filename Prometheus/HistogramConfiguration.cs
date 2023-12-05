namespace Prometheus;

public sealed class HistogramConfiguration : MetricConfiguration
{
    internal static readonly HistogramConfiguration Default = new HistogramConfiguration();

    /// <summary>
    /// Custom histogram buckets to use. If null, will use Histogram.DefaultBuckets.
    /// </summary>
    public double[]? Buckets { get; set; }

    /// <summary>
    /// Allows you to configure how exemplars are applied to the published metric.
    /// If null, inherits the exemplar behavior from the metric factory.
    /// </summary>
    public ExemplarBehavior? ExemplarBehavior { get; set; }
}
