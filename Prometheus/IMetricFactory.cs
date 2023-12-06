using System.ComponentModel;

namespace Prometheus;

/// <summary>
/// Allows for substitution of MetricFactory in tests.
/// You cannot provide your own implementation to prometheus-net code, only to your own code.
/// </summary>
public interface IMetricFactory
{
    // These require you to allocate a Configuration for each instance, which can be wasteful because often the only thing that differs is the label names.
    // We will mark them as non-browsable to discourage their use. They still work, so they are not obsolete or anything like that. Just discouraged.
    [EditorBrowsable(EditorBrowsableState.Never)]
    Counter CreateCounter(string name, string help, CounterConfiguration? configuration = null);
    [EditorBrowsable(EditorBrowsableState.Never)]
    Gauge CreateGauge(string name, string help, GaugeConfiguration? configuration = null);
    [EditorBrowsable(EditorBrowsableState.Never)]
    Histogram CreateHistogram(string name, string help, HistogramConfiguration? configuration = null);
    [EditorBrowsable(EditorBrowsableState.Never)]
    Summary CreateSummary(string name, string help, SummaryConfiguration? configuration = null);

    // These allow you to reuse a Configuration and only provide the label names. The reduced memory allocations can make a difference in high performance scenarios.
    // If label names are provided in both, they must match. Otherwise, label names in the Configuration object may be null.
    Counter CreateCounter(string name, string help, string[] labelNames, CounterConfiguration? configuration = null);
    Gauge CreateGauge(string name, string help, string[] labelNames, GaugeConfiguration? configuration = null);
    Histogram CreateHistogram(string name, string help, string[] labelNames, HistogramConfiguration? configuration = null);
    Summary CreateSummary(string name, string help, string[] labelNames, SummaryConfiguration? configuration = null);

    /// <summary>
    /// Returns a new metric factory that will add the specified labels to any metrics created using it.
    /// </summary>
    IMetricFactory WithLabels(IDictionary<string, string> labels);

    /// <summary>
    /// Returns a factory that creates metrics with a managed lifetime.
    /// </summary>
    /// <param name="expiresAfter">
    /// Metrics created from this factory will expire after this time span elapses, enabling automatic deletion of unused metrics.
    /// The expiration timer is reset to zero for the duration of any active lifetime-extension lease that is taken on a specific metric.
    /// </param>
    IManagedLifetimeMetricFactory WithManagedLifetime(TimeSpan expiresAfter);

    /// <summary>
    /// Allows you to configure how exemplars are applied to published metrics. If null, uses default behavior (see <see cref="ExemplarBehavior"/>).
    /// This is inherited by all metrics by default, although may be overridden in the configuration of an individual metric.
    /// </summary>
    ExemplarBehavior? ExemplarBehavior { get; set; }
}
