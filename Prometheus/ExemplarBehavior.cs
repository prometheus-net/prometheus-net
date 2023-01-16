namespace Prometheus;

/// <summary>
/// Defines how exemplars are obtained and published for metrics.
/// Different metrics can have their own exemplar behavior or simply inherit one from the metric factory.
/// </summary>
public sealed class ExemplarBehavior
{
    /// <summary>
    /// Callback that provides the default exemplar if none is provided by the caller when providing a metric value.
    /// Defaults to Exemplar.FromTraceContext().
    /// </summary>
    public ExemplarProvider? DefaultExemplarProvider { get; set; }

    internal static readonly ExemplarBehavior Default = new ExemplarBehavior
    {
        DefaultExemplarProvider = (_, _) => Exemplar.FromTraceContext()
    };
}
