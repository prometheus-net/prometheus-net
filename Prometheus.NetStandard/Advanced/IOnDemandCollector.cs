namespace Prometheus.Advanced
{
    /// <summary>
    /// An on-demand collector is a mechanism that enables one or more metrics to be updated before each data collection.
    /// 
    /// It is by itself not a collector in Prometheus terms, as it does not produce any data, simply manages other collectors.
    /// </summary>
    public interface IOnDemandCollector
    {
        /// <summary>
        /// Called when the instance is associated with a collector registry, so that the collectors managed
        /// by this instance can be registered. Note that collectors can be registered with more than one registry.
        /// </summary>
        void RegisterMetrics(ICollectorRegistry registry);

        /// <summary>
        /// Called before each collection. Any values in collectors managed by this instance should now be brought up to date.
        /// 
        /// If this throws <see cref="ScrapeFailedException"/> then the collection will fail (not only from this collector
        /// but globally). This will result in an appropriate HTTP error code or a skipped push, depending on type of exporter.
        /// </summary>
        void UpdateMetrics();
    }
}