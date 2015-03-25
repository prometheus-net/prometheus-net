using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Prometheus.Internal
{
    internal class MetricsRegistry 
    {
        public readonly static MetricsRegistry Instance = new MetricsRegistry();

        readonly ConcurrentDictionary<string, MetricFamily> _metrics = new ConcurrentDictionary<string, MetricFamily>();

        public MetricFamily GetOrAdd(string name, string help, string[] labelNames, Type metricType)
        {
            var result = _metrics.GetOrAdd(name, s => new MetricFamily(name, help, metricType, labelNames));
            if (metricType!=result.MetricType)
            {
                throw new InvalidOperationException(string.Format("A metric of type {0} has already been declared with name '{1}'", result.MetricType.Name, name));
            }
            return result;
        }

        public IEnumerable<io.prometheus.client.MetricFamily> CollectAll()
        {
            return _metrics.Values.Select(value => value.Collect());
        }

        public void Clear()
        {
            _metrics.Clear();
        }
    }
}