namespace Prometheus;

/// <summary>
/// A metric factory for creating metrics that use a managed lifetime, whereby the metric may
/// be deleted based on logic other than disposal or similar explicit deletion.
/// </summary>
/// <remarks>
/// The lifetime management logic is associated with a metric handle. Calling CreateXyz() with equivalent identity parameters will return
/// the same handle. However, using multiple factories will create independent handles (which will delete the same metric independently).
/// </remarks>
public interface IManagedLifetimeMetricFactory
{
    /// <summary>
    /// Creates a metric with a lease-extended lifetime.
    /// A timeseries will expire N seconds after the last lease is released, with N determined at factory create-time.
    /// </summary>
    IManagedLifetimeMetricHandle<ICounter> CreateCounter(string name, string help, string[]? labelNames = null, CounterConfiguration? configuration = null);

    /// <summary>
    /// Creates a metric with a lease-extended lifetime.
    /// A timeseries will expire N seconds after the last lease is released, with N determined at factory create-time.
    /// </summary>
    IManagedLifetimeMetricHandle<IGauge> CreateGauge(string name, string help, string[]? labelNames = null, GaugeConfiguration? configuration = null);

    /// <summary>
    /// Creates a metric with a lease-extended lifetime.
    /// A timeseries will expire N seconds after the last lease is released, with N determined at factory create-time.
    /// </summary>
    IManagedLifetimeMetricHandle<IHistogram> CreateHistogram(string name, string help, string[]? labelNames = null, HistogramConfiguration? configuration = null);

    /// <summary>
    /// Creates a metric with a lease-extended lifetime.
    /// A timeseries will expire N seconds after the last lease is released, with N determined at factory create-time.
    /// </summary>
    IManagedLifetimeMetricHandle<ISummary> CreateSummary(string name, string help, string[]? labelNames = null, SummaryConfiguration? configuration = null);

    /// <summary>
    /// Returns a new metric factory that will add the specified labels to any metrics created using it.
    /// Different instances returned for the same labels are equivalent and any metrics created via them share their lifetimes.
    /// </summary>
    IManagedLifetimeMetricFactory WithLabels(IDictionary<string, string> labels);
}
