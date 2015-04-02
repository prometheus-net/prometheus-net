using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Prometheus.Advanced
{
    public class DefaultCollectorRegistry : ICollectorRegistry
    {
        public readonly static DefaultCollectorRegistry Instance = new DefaultCollectorRegistry();

        readonly ConcurrentDictionary<string, ICollector> _collectorsByName = new ConcurrentDictionary<string, ICollector>();

        public IEnumerable<MetricFamily> CollectAll()
        {
            return _collectorsByName.Values.Select(value => value.Collect());
        }

        public void Clear()
        {
            _collectorsByName.Clear();
        }

        public void Register(ICollector collector)
        {
            if (_collectorsByName.ContainsKey(collector.Name))
            {
                throw new InvalidOperationException(string.Format("A collector with name '{0}' has already been registered!", collector.Name));
            }

            _collectorsByName[collector.Name] = collector;
        }

        public void Remove(ICollector collector)
        {
            ICollector value;
            _collectorsByName.TryRemove(collector.Name, out value);
        }
    }
}