namespace Prometheus;

public interface ICounter : ICollectorChild
{
    /// <summary>
    /// Increment a counter by 1.
    /// </summary>
    void Inc(double increment = 1.0);

    /// <summary>
    /// Increment a counter by 1.
    /// </summary>
    /// <param name="exemplar">
    /// A set of labels representing an exemplar, created using Exemplar.From().
    /// If null, the default exemplar provider associated with the metric is asked to provide an exemplar.
    /// Pass Exemplar.None to explicitly record an observation without an exemplar.
    /// </param>
    void Inc(Exemplar? exemplar);

    /// <summary>
    /// Increment a counter.
    /// </summary>
    /// <param name="increment">The increment.</param>
    /// <param name="exemplar">
    /// A set of labels representing an exemplar, created using Exemplar.From().
    /// If null, the default exemplar provider associated with the metric is asked to provide an exemplar.
    /// Pass Exemplar.None to explicitly record an observation without an exemplar.
    /// </param>
    void Inc(double increment, Exemplar? exemplar);

    void IncTo(double targetValue);

    double Value { get; }
}
