using Prometheus.DataContracts;
using System.Collections.Generic;

namespace Prometheus
{
    /// <summary>
    /// A collector registry maintains a set of metrics for export.
    /// Most apps will use a single collector registry, where all their metrics are registered.
    /// 
    /// <see cref="DefaultCollectorRegistry"/> provides a default implementation and a singleton instance.
    /// </summary>
    public interface ICollectorRegistry
    {
        ICollector GetOrAdd(ICollector collector);
        bool Remove(ICollector collector);

        IEnumerable<MetricFamily> CollectAll();
    }
}