namespace Prometheus
{
    /// <summary>
    /// A metric factory for creating metrics that use a managed lifetime, whereby the metric may
    /// be unpublished based on logic other than disposal or similar explicit unpublishing.
    /// </summary>
    public interface IManagedLifetimeMetricFactory
    {
        /// <summary>
        /// Creates a metric with a lease-extended lifetime.
        /// A timeseries will expire N seconds after the last lease is released, with N determined at factory create-time.
        /// </summary>
        ILeasedLifetimeMetric<ICounter> CreateCounter(string name, string help, CounterConfiguration? configuration = null);

        /// <summary>
        /// Creates a metric with a lease-extended lifetime.
        /// A timeseries will expire N seconds after the last lease is released, with N determined at factory create-time.
        /// </summary>
        ILeasedLifetimeMetric<IGauge> CreateGauge(string name, string help, GaugeConfiguration? configuration = null);

        /// <summary>
        /// Creates a metric with a lease-extended lifetime.
        /// A timeseries will expire N seconds after the last lease is released, with N determined at factory create-time.
        /// </summary>
        ILeasedLifetimeMetric<IHistogram> CreateHistogram(string name, string help, HistogramConfiguration? configuration = null);

        /// <summary>
        /// Creates a metric with a lease-extended lifetime.
        /// A timeseries will expire N seconds after the last lease is released, with N determined at factory create-time.
        /// </summary>
        ILeasedLifetimeMetric<ISummary> CreateSummary(string name, string help, SummaryConfiguration? configuration = null);
    }
}
