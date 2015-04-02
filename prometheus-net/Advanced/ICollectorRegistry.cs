using System.Collections.Generic;

namespace Prometheus.Advanced
{
    public interface ICollectorRegistry
    {
        void Register(ICollector collector);
        void Remove(ICollector collector);
        IEnumerable<MetricFamily> CollectAll();
    }
}