using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Prometheus
{
    /// <summary>
    /// Maintains references to a set of collectors, from which data for metrics is collected at data export time.
    /// 
    /// Use methods on the <see cref="Metrics"/> class to add metrics to a collector registry.
    /// </summary>
    /// <remarks>
    /// To encourage good concurrency practices, registries are append-only. You can add things to them but not remove.
    /// If you wish to remove things from the registry, create a new registry with only the things you wish to keep.
    /// </remarks>
    public sealed class CollectorRegistry
    {
        /// <summary>
        /// Registers an action to be called before metrics are collected.
        /// This enables you to do last-minute updates to metric values very near the time of collection.
        /// 
        /// If the callback throws <see cref="ScrapeFailedException"/> then the entire metric collection will fail.
        /// This will result in an appropriate HTTP error code or a skipped push, depending on type of exporter.
        /// </summary>
        public void AddBeforeCollectCallback(Action callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            _beforeCollectCallbacks.Add(callback);
        }

        private readonly ConcurrentBag<Action> _beforeCollectCallbacks = new ConcurrentBag<Action>();

        // We pass this thing to GetOrAdd to avoid allocating a collector or a closure.
        // This reduces memory usage in situations where the collector is already registered.
        internal readonly struct CollectorInitializer<TCollector, TConfiguration>
            where TCollector : Collector
            where TConfiguration : MetricConfiguration
        {
            private readonly Func<string, string, TConfiguration, TCollector> _createInstance;
            private readonly string _name;
            private readonly string _help;
            private readonly TConfiguration _configuration;

            public string Name => _name;
            public TConfiguration Configuration => _configuration;

            public CollectorInitializer(Func<string, string, TConfiguration, TCollector> createInstance,
                string name, string help, TConfiguration configuration)
            {
                _createInstance = createInstance;
                _name = name;
                _help = help;
                _configuration = configuration;
            }

            public TCollector CreateInstance(string _) => _createInstance(_name, _help, _configuration);
        }

        /// <summary>
        /// Adds a collector to the registry, returning an existing instance if one with a matching name was already registered.
        /// </summary>
        internal TCollector GetOrAdd<TCollector, TConfiguration>(in CollectorInitializer<TCollector, TConfiguration> initializer)
            where TCollector : Collector
            where TConfiguration : MetricConfiguration
        {
            var collectorToUse = _collectors.GetOrAdd(initializer.Name, initializer.CreateInstance);

            if (!(collectorToUse is TCollector))
                throw new InvalidOperationException("Collector of a different type with the same name is already registered.");

            if ((initializer.Configuration.LabelNames?.Length ?? 0) != collectorToUse.LabelNames.Length
                || (!initializer.Configuration.LabelNames?.SequenceEqual(collectorToUse.LabelNames) ?? false))
                throw new InvalidOperationException("Collector with same name must have same label names");

            return (TCollector)collectorToUse;
        }

        private readonly ConcurrentDictionary<string, Collector> _collectors = new ConcurrentDictionary<string, Collector>();

        /// <summary>
        /// Collects metrics from all the registered collectors and sends them to the specified serializer.
        /// </summary>
        internal void CollectAndSerialize(IMetricsSerializer serializer)
        {
            foreach (var callback in _beforeCollectCallbacks)
                callback();

            foreach (var collector in _collectors.Values)
                collector.CollectAndSerialize(serializer);
        }
    }
}
