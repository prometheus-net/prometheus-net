using Prometheus.Advanced.DataContracts;
using System.Collections.Generic;

namespace Prometheus.Advanced
{
    public interface ICollectorRegistry
    {
        ICollector GetOrAdd(ICollector collector);
        bool Remove(ICollector collector);
        IEnumerable<MetricFamily> CollectAll();
    }
}