using Prometheus.Advanced.DataContracts;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Prometheus.Advanced
{
    public interface ICollectorRegistry
    {
        ICollector GetOrAdd(ICollector collector);
        bool Remove(ICollector collector);

        IEnumerable<MetricFamily> CollectAll(NameValueCollection queryParameters = null);
    }
}
