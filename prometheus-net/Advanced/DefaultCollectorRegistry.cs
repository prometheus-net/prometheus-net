using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Prometheus.Advanced.DataContracts;

namespace Prometheus.Advanced
{
    public class DefaultCollectorRegistry : ICollectorRegistry
    {
        public readonly static DefaultCollectorRegistry Instance = new DefaultCollectorRegistry();
        private readonly ConcurrentDictionary<string, ICollector> _collectors = new ConcurrentDictionary<string, ICollector>();
        private readonly List<IOnDemandCollector> _onDemandCollectors = new List<IOnDemandCollector>();

        public void RegisterOnDemandCollectors(IEnumerable<IOnDemandCollector> onDemandCollectors)
        {
            _onDemandCollectors.AddRange(onDemandCollectors);

            foreach (var onDemandCollector in _onDemandCollectors)
            {
                onDemandCollector.RegisterMetrics();
            }
        }

        public IEnumerable<MetricFamily> CollectAll()
        {
            foreach (var onDemandCollector in _onDemandCollectors)
            {
                onDemandCollector.UpdateMetrics();
            }
            
            foreach (var value in _collectors.Values)
            {
                var c = value.Collect();
                if (c != null) yield return c;
            }
        }

        public void Clear()
        {
            _collectors.Clear();
        }

        public ICollector GetOrAdd(ICollector collector)
        {
            var collectorToUse = _collectors.GetOrAdd(collector.Name, collector);

            if (!collector.LabelNames.SequenceEqual(collectorToUse.LabelNames))
                throw new InvalidOperationException("Collector with same name must have same label names");

            return collectorToUse;
        }

        public bool Remove(ICollector collector)
        {
            ICollector dummy;
            return _collectors.TryRemove(collector.Name, out dummy);
        }
    }
}