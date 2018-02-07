using Prometheus;
using Prometheus.Advanced;

namespace tester
{
    public sealed class OnDemandCollection : IOnDemandCollector
    {
        private Counter _collectionCount;

        public void RegisterMetrics(ICollectorRegistry registry)
        {
            _collectionCount = Metrics.WithCustomRegistry(registry)
                .CreateCounter("ondemand_counter_example", "This counter is incremented before every data collection.");
        }

        public void UpdateMetrics()
        {
            _collectionCount.Inc();
        }
    }
}
