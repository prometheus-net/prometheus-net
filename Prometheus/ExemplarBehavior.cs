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

    /// <summary>
    /// A new exemplar will only be recorded for a timeseries if at least this much time has passed since the previous exemplar was recorded.
    /// This can be used to limit the rate of publishing unique exemplars. By default we do not have any limit - a new exemplar always overwrites the old one.
    /// </summary>
    public TimeSpan NewExemplarMinInterval { get; set; } = TimeSpan.Zero;

    internal static readonly ExemplarBehavior Default = new()
    {
        DefaultExemplarProvider = (_, _) => Exemplar.FromTraceContext()
    };

    public static ExemplarBehavior NoExemplars() => new()
    {
        DefaultExemplarProvider = (_, _) => Exemplar.None
    };
}
