namespace Prometheus;

public sealed class CounterConfiguration : MetricConfiguration
{
    internal static readonly CounterConfiguration Default = new();

    /// <summary>
    /// Allows you to configure how exemplars are applied to the published metric.
    /// If null, inherits the exemplar behavior from the metric factory.
    /// </summary>
    public ExemplarBehavior? ExemplarBehavior { get; set; }
}
