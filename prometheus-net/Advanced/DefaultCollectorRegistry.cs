using System;
using System.Collections.Generic;
using System.Linq;
using Prometheus.Advanced.DataContracts;

namespace Prometheus.Advanced
{
    public class DefaultCollectorRegistry : ICollectorRegistry
    {
        public readonly static DefaultCollectorRegistry Instance = new DefaultCollectorRegistry();

        /// <summary>
        /// a list with copy-on-write semantics implemented in-place below. This is to avoid any locks on the read side (ie, CollectAll())
        /// </summary>
        private List<ICollector> _collectors = new List<ICollector>();

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
            foreach (ICollector value in _collectors)
            {
                MetricFamily c = value.Collect();
                if (c != null) yield return c;
            }
        }

        public void Clear()
        {
            lock (_collectors)
            {
                _collectors = new List<ICollector>();
            }
        }

        public void Register(ICollector collector)
        {
            if (_collectors.Any(c => c.Name == collector.Name))
            {
                throw new InvalidOperationException(string.Format("A collector with name '{0}' has already been registered!", collector.Name));
            }

            lock (_collectors)
            {
                var newList = new List<ICollector>(_collectors);
                newList.Add(collector);
                _collectors = newList;
            }
        }

        public bool Remove(ICollector collector)
        {
            lock (_collectors)
            {
                var newList = new List<ICollector>(_collectors);
                bool removed = newList.Remove(collector);
                _collectors = newList;
                return removed;
            }
        }
    }
}