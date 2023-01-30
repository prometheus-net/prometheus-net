namespace Prometheus;

/// <summary>
/// Callback to provide an exemplar for a specific observation.
/// </summary>
/// <param name="metric">The metric instance for which an exemplar is being provided.</param>
/// <param name="value">Context-dependent - for counters, the increment; for histograms, the observed value.</param>
public delegate Exemplar ExemplarProvider(Collector metric, double value);
