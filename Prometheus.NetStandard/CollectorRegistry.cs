using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        /// Callbacks will delay the metric collection, so do not make them too long or it may time out.
        /// 
        /// The callback will be executed synchronously and should not take more than a few milliseconds.
        /// To execute longer-duration callbacks, register an asynchronous callback (Func&lt;Task&gt;).
        /// 
        /// If the callback throws <see cref="ScrapeFailedException"/> then the entire metric collection will fail.
        /// This will result in an appropriate HTTP error code or a skipped push, depending on type of exporter.
        /// 
        /// If multiple concurrent collections occur, the callback may be called multiple times concurrently.
        /// </summary>
        public void AddBeforeCollectCallback(Action callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            _beforeCollectCallbacks.Add(callback);
        }

        /// <summary>
        /// Registers an action to be called before metrics are collected.
        /// This enables you to do last-minute updates to metric values very near the time of collection.
        /// Callbacks will delay the metric collection, so do not make them too long or it may time out.
        /// 
        /// Asynchronous callbacks will be executed concurrently and may last longer than a few milliseconds.
        /// 
        /// If the callback throws <see cref="ScrapeFailedException"/> then the entire metric collection will fail.
        /// This will result in an appropriate HTTP error code or a skipped push, depending on type of exporter.
        /// 
        /// If multiple concurrent collections occur, the callback may be called multiple times concurrently.
        /// </summary>
        public void AddBeforeCollectCallback(Func<CancellationToken, Task> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            _beforeCollectAsyncCallbacks.Add(callback);
        }

        /// <summary>
        /// Collects all metrics and exports them in text document format to the provided stream.
        /// 
        /// This method is designed to be used with custom output mechanisms that do not use an IMetricServer.
        /// </summary>
        public Task CollectAndExportAsTextAsync(Stream to, CancellationToken cancel = default)
        {
            if (to == null)
                throw new ArgumentNullException(nameof(to));

            return CollectAndSerializeAsync(new TextSerializer(to), cancel);
        }

        private readonly ConcurrentBag<Action> _beforeCollectCallbacks = new ConcurrentBag<Action>();
        private readonly ConcurrentBag<Func<CancellationToken, Task>> _beforeCollectAsyncCallbacks = new ConcurrentBag<Func<CancellationToken, Task>>();

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
                throw new InvalidOperationException("Collector matches a previous registration but has a different set of label names.");

            return (TCollector)collectorToUse;
        }

        private readonly ConcurrentDictionary<string, Collector> _collectors = new ConcurrentDictionary<string, Collector>();

        internal void SetBeforeFirstCollectCallback(Action a)
        {
            lock (_firstCollectLock)
            {
                if (_hasPerformedFirstCollect)
                    return; // Avoid keeping a reference to a callback we won't ever use.

                _beforeFirstCollectCallback = a;
            }
        }

        /// <summary>
        /// Allows us to initialize (or not) the registry with the default metrics before the first collection.
        /// </summary>
        private Action? _beforeFirstCollectCallback;
        private bool _hasPerformedFirstCollect;
        private readonly object _firstCollectLock = new object();

        /// <summary>
        /// Collects metrics from all the registered collectors and sends them to the specified serializer.
        /// </summary>
        internal async Task CollectAndSerializeAsync(IMetricsSerializer serializer, CancellationToken cancel)
        {
            lock (_firstCollectLock)
            {
                if (!_hasPerformedFirstCollect)
                {
                    _hasPerformedFirstCollect = true;
                    _beforeFirstCollectCallback?.Invoke();
                    _beforeFirstCollectCallback = null;
                }
            }

            foreach (var callback in _beforeCollectCallbacks)
                callback();

            await Task.WhenAll(_beforeCollectAsyncCallbacks.Select(callback => callback(cancel)));

            foreach (var collector in _collectors.Values)
                await collector.CollectAndSerializeAsync(serializer, cancel);

            await serializer.FlushAsync(cancel);
        }
    }
}
