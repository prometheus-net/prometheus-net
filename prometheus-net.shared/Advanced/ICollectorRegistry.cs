using System.Collections.Generic;
using Prometheus.Advanced.DataContracts;

namespace Prometheus.Advanced
{
    public interface ICollectorRegistry
    {
        ICollector GetOrAdd(ICollector collector);
        bool Remove(ICollector collector);
        IEnumerable<MetricFamily> CollectAll();
    }
}