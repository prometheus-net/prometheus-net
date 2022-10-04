namespace Prometheus
{
    /// <summary>
    /// A metric factory for creating metrics that use a managed lifetime, whereby the metric may
    /// be unpublished based on logic other than disposal or similar explicit unpublishing.
    /// </summary>
    /// <remarks>
    /// The lifetime management logic is associated with a metric handle. Calling CreateXyz() with equivalent identity parameters will return
    /// the same handle. However, using multiple factories will create independent handles (which will unpublish the same metric independently).
    /// </remarks>
    public interface IManagedLifetimeMetricFactory
    {
        /// <summary>
        /// Creates a metric with a lease-extended lifetime.
        /// A timeseries will expire N seconds after the last lease is released, with N determined at factory create-time.
        /// </summary>
        IManagedLifetimeMetricHandle<ICounter> CreateCounter(string name, string help, string[] labelNames, CounterConfiguration? configuration = null);

        /// <summary>
        /// Creates a metric with a lease-extended lifetime.
        /// A timeseries will expire N seconds after the last lease is released, with N determined at factory create-time.
        /// </summary>
        IManagedLifetimeMetricHandle<IGauge> CreateGauge(string name, string help, string[] labelNames, GaugeConfiguration? configuration = null);

        /// <summary>
        /// Creates a metric with a lease-extended lifetime.
        /// A timeseries will expire N seconds after the last lease is released, with N determined at factory create-time.
        /// </summary>
        IManagedLifetimeMetricHandle<IHistogram> CreateHistogram(string name, string help, string[] labelNames, HistogramConfiguration? configuration = null);

        /// <summary>
        /// Creates a metric with a lease-extended lifetime.
        /// A timeseries will expire N seconds after the last lease is released, with N determined at factory create-time.
        /// </summary>
        IManagedLifetimeMetricHandle<ISummary> CreateSummary(string name, string help, string[] labelNames, SummaryConfiguration? configuration = null);
    }
}
