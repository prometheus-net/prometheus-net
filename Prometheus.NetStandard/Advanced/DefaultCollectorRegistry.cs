using Prometheus.Advanced.DataContracts;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Prometheus.Advanced
{
    public class DefaultCollectorRegistry : ICollectorRegistry
    {
        /// <summary>
        /// The singleton registry used by default when the caller does not specify a custom/specific registry.
        /// </summary>
        public static readonly DefaultCollectorRegistry Instance;

        static DefaultCollectorRegistry()
        {
            // We register the default on-demand collectors here. To avoid having the,
            // use a custom instance instead of the singleton or call Clear() before the first use.
            Instance = new DefaultCollectorRegistry();

            Instance.RegisterOnDemandCollector<DotNetStatsCollector>();
        }

        private readonly ConcurrentDictionary<string, ICollector> _collectors = new ConcurrentDictionary<string, ICollector>();
        private readonly ConcurrentBag<IOnDemandCollector> _onDemandCollectors = new ConcurrentBag<IOnDemandCollector>();

        public void RegisterOnDemandCollector<TCollector>() where TCollector : class, IOnDemandCollector, new()
        {
            RegisterOnDemandCollectors(new TCollector());
        }

        public void RegisterOnDemandCollectors(params IOnDemandCollector[] onDemandCollectors)
        {
            RegisterOnDemandCollectors((IEnumerable<IOnDemandCollector>)onDemandCollectors);
        }

        public void RegisterOnDemandCollectors(IEnumerable<IOnDemandCollector> onDemandCollectors)
        {
            foreach (var collector in onDemandCollectors)
            {
                _onDemandCollectors.Add(collector);
            }

            foreach (var onDemandCollector in _onDemandCollectors)
            {
                onDemandCollector.RegisterMetrics(this);
            }
        }

        public IEnumerable<MetricFamily> CollectAll()
        {
            // We need to do all updates before constructing the iterator, so we do not
            // perform a lazy update too late in the collection cycle to react to failures.
            foreach (var onDemandCollector in _onDemandCollectors)
            {
                onDemandCollector.UpdateMetrics();
            }

            return CollectAllIterator();
        }

        private IEnumerable<MetricFamily> CollectAllIterator()
        {
            foreach (var collector in _collectors.Values)
            {
                foreach (var family in collector.Collect())
                    yield return family;
            }
        }

        /// <summary>
        /// Clears all collectors and on-demand collectors from the registry.
        /// </summary>
        public void Clear()
        {
            _collectors.Clear();

            while (_onDemandCollectors.Count > 0)
                _onDemandCollectors.TryTake(out _);
        }

        public ICollector GetOrAdd(ICollector collector)
        {
            var key = $"{collector.Name}|{string.Join("|", collector.LabelNames ?? new string[] { })}";
            var collectorToUse = _collectors.GetOrAdd(key, collector);
            return collectorToUse;
        }

        public bool Remove(ICollector collector)
        {
            return _collectors.TryRemove(collector.Name, out _);
        }
    }
}