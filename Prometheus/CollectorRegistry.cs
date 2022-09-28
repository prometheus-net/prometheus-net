using System.Collections.Concurrent;
using System.Collections.ObjectModel;

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
    public sealed class CollectorRegistry : ICollectorRegistry
    {
        #region "Before collect" callbacks
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

        private readonly ConcurrentBag<Action> _beforeCollectCallbacks = new ConcurrentBag<Action>();
        private readonly ConcurrentBag<Func<CancellationToken, Task>> _beforeCollectAsyncCallbacks = new ConcurrentBag<Func<CancellationToken, Task>>();
        #endregion

        #region Static labels
        /// <summary>
        /// The set of static labels that are applied to all metrics in this registry.
        /// Enumeration of the returned collection is thread-safe.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> StaticLabels => _staticLabels;

        /// <summary>
        /// Defines the set of static labels to apply to all metrics in this registry.
        /// The static labels can only be set once on startup, before adding or publishing any metrics.
        /// </summary>
        public void SetStaticLabels(Dictionary<string, string> labels)
        {
            if (labels == null)
                throw new ArgumentNullException(nameof(labels));

            // Read lock is taken when creating metrics, so we know that no metrics can be created while we hold this lock.
            _staticLabelsLock.EnterWriteLock();

            try
            {
                if (_staticLabels.Length != 0)
                    throw new InvalidOperationException("Static labels have already been defined - you can only do it once per registry.");

                if (_collectors.Count != 0)
                    throw new InvalidOperationException("Metrics have already been added to the registry - cannot define static labels anymore.");

                // Keep the lock for the duration of this method to make sure no publishing happens while we are setting labels.
                lock (_firstCollectLock)
                {
                    if (_hasPerformedFirstCollect)
                        throw new InvalidOperationException("The metrics registry has already been published - cannot define static labels anymore.");

                    foreach (var pair in labels)
                    {
                        if (pair.Key == null)
                            throw new ArgumentException("The name of a label cannot be null.");

                        if (pair.Value == null)
                            throw new ArgumentException("The value of a label cannot be null.");

                        Collector.ValidateLabelName(pair.Key);
                    }

                    _staticLabels = labels.ToArray();
                }
            }
            finally
            {
                _staticLabelsLock.ExitWriteLock();
            }
        }

        private KeyValuePair<string, string>[] _staticLabels = new KeyValuePair<string, string>[0];
        private readonly ReaderWriterLockSlim _staticLabelsLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Executes an action while holding a read lock on the set of static labels (provided as parameter).
        /// </summary>
        internal TReturn WhileReadingStaticLabels<TReturn>(Func<Labels, TReturn> action)
        {
            _staticLabelsLock.EnterReadLock();

            try
            {
                var labels = new Labels(_staticLabels.Select(item => item.Key).ToArray(), _staticLabels.Select(item => item.Value).ToArray());

                return action(labels);
            }
            finally
            {
                _staticLabelsLock.ExitReadLock();
            }
        }
        #endregion

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

            public TCollector CreateInstance(CollectorIdentity _) => _createInstance(_name, _help, _configuration);
        }

        /// <summary>
        /// Adds a collector to the registry, returning an existing instance if one with a matching name was already registered.
        /// </summary>
        internal TCollector GetOrAdd<TCollector, TConfiguration>(in CollectorInitializer<TCollector, TConfiguration> initializer)
            where TCollector : Collector
            where TConfiguration : MetricConfiguration
        {
            var identity = new CollectorIdentity(initializer.Name, initializer.Configuration.LabelNames ?? Array.Empty<string>());

            var collector = _collectors.GetOrAdd(identity, initializer.CreateInstance);

            // We either created a new collector or found one with a matching identity.
            // We do some basic validation here to avoid silly API usage mistakes.

            if (!(collector is TCollector))
                throw new InvalidOperationException("Collector of a different type with the same identity is already registered.");

            return (TCollector)collector;
        }

        private readonly ConcurrentDictionary<CollectorIdentity, Collector> _collectors = new ConcurrentDictionary<CollectorIdentity, Collector>();

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

            UpdateRegistryMetrics();

            foreach (var collector in _collectors.Values)
                await collector.CollectAndSerializeAsync(serializer, cancel);

            await serializer.FlushAsync(cancel);
        }

        /// <summary>
        /// We collect some debug metrics from the registry itself to help indicate how many metrics we are publishing.
        /// </summary>
        internal void StartCollectingRegistryMetrics()
        {
            var factory = Metrics.WithCustomRegistry(this);

            _metricFamilies = factory.CreateGauge("prometheus_net_metric_families", "Number of metric families currently registered.", new GaugeConfiguration
            {
                LabelNames = new[] { MetricTypeDebugLabel }
            });
            _metricInstances = factory.CreateGauge("prometheus_net_metric_instances", "Number of metric instances currently registered across all metric families.", new GaugeConfiguration
            {
                LabelNames = new[] { MetricTypeDebugLabel }
            });
            _metricTimeseries = factory.CreateGauge("prometheus_net_metric_timeseries", "Number of metric timeseries currently generated from all metric instances.", new GaugeConfiguration
            {
                LabelNames = new[] { MetricTypeDebugLabel }
            });

            _metricFamiliesPerType = new();
            _metricInstancesPerType = new();
            _metricTimeseriesPerType = new();

            foreach (MetricType type in Enum.GetValues(typeof(MetricType)))
            {
                var typeName = type.ToString().ToLowerInvariant();
                _metricFamiliesPerType[type] = _metricFamilies.WithLabels(typeName);
                _metricInstancesPerType[type] = _metricInstances.WithLabels(typeName);
                _metricTimeseriesPerType[type] = _metricTimeseries.WithLabels(typeName);
            }
        }

        private const string MetricTypeDebugLabel = "metric_type";

        private Gauge? _metricFamilies;
        private Gauge? _metricInstances;
        private Gauge? _metricTimeseries;

        private Dictionary<MetricType, Gauge.Child>? _metricFamiliesPerType;
        private Dictionary<MetricType, Gauge.Child>? _metricInstancesPerType;
        private Dictionary<MetricType, Gauge.Child>? _metricTimeseriesPerType;

        private void UpdateRegistryMetrics()
        {
            if (_metricFamiliesPerType == null ||_metricInstancesPerType == null || _metricTimeseriesPerType == null)
                return; // Debug metrics are not enabled.

            foreach (MetricType type in Enum.GetValues(typeof(MetricType)))
            {
                long families = 0;
                long instances = 0;
                long timeseries = 0;

                foreach (var collector in _collectors.Values.Where(c => c.Type == type))
                {
                    families++;
                    instances += collector.ChildCount;
                    timeseries += collector.TimeseriesCount;
                }

                _metricFamiliesPerType[type].Set(families);
                _metricInstancesPerType[type].Set(instances);
                _metricTimeseriesPerType[type].Set(timeseries);
            }
        }
    }
}
