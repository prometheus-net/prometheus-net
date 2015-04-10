using System.Collections.Generic;
using Prometheus.Advanced.DataContracts;

namespace Prometheus.Advanced
{
    public interface ICollectorRegistry
    {
        void Register(ICollector collector);
        bool Remove(ICollector collector);
        IEnumerable<MetricFamily> CollectAll();
    }
}