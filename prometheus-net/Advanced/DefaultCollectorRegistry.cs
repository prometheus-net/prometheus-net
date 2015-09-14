using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Prometheus.Advanced.DataContracts;

namespace Prometheus.Advanced
{
    public class DefaultCollectorRegistry : ICollectorRegistry
    {
        public readonly static DefaultCollectorRegistry Instance = new DefaultCollectorRegistry();
        
        private readonly ConcurrentDictionary<string, ICollector> _collectors = new ConcurrentDictionary<string, ICollector>();

        public void RegisterStandardPerfCounters()
        {
            var perfCounterCollector = new PerfCounterCollector();
            Register(perfCounterCollector);
            perfCounterCollector.RegisterStandardPerfCounters();
        }

        public IEnumerable<MetricFamily> CollectAll()
        {
            //return _collectors.Select(value => value.Collect()).Where(c=>c != null);
            
            //replaced LINQ with code to avoid extra allocations
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

        public void Register(ICollector collector)
        {
            if (!_collectors.TryAdd(collector.Name, collector))
                throw new InvalidOperationException(string.Format("A collector with name '{0}' has already been registered!", collector.Name));
        }

        public bool Remove(ICollector collector)
        {
            ICollector dummy;
            return _collectors.TryRemove(collector.Name, out dummy);
        }
    }
}