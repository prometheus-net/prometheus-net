using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Prometheus;

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

    private readonly ConcurrentBag<Action> _beforeCollectCallbacks = [];
    private readonly ConcurrentBag<Func<CancellationToken, Task>> _beforeCollectAsyncCallbacks = [];
    #endregion

    #region Static labels
    /// <summary>
    /// The set of static labels that are applied to all metrics in this registry.
    /// Enumeration of the returned collection is thread-safe.
    /// </summary>
    public IEnumerable<KeyValuePair<string, string>> StaticLabels => _staticLabels.ToDictionary();

    /// <summary>
    /// Defines the set of static labels to apply to all metrics in this registry.
    /// The static labels can only be set once on startup, before adding or publishing any metrics.
    /// </summary>
    public void SetStaticLabels(IDictionary<string, string> labels)
    {
        if (labels == null)
            throw new ArgumentNullException(nameof(labels));

        // Read lock is taken when creating metrics, so we know that no metrics can be created while we hold this lock.
        _staticLabelsLock.EnterWriteLock();

        try
        {
            if (_staticLabels.Length != 0)
                throw new InvalidOperationException("Static labels have already been defined - you can only do it once per registry.");

            if (_families.Count != 0)
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

                _staticLabels = LabelSequence.From(labels);
            }
        }
        finally
        {
            _staticLabelsLock.ExitWriteLock();
        }
    }

    private LabelSequence _staticLabels;
    private readonly ReaderWriterLockSlim _staticLabelsLock = new();

    internal LabelSequence GetStaticLabels()
    {
        _staticLabelsLock.EnterReadLock();

        try
        {
            return _staticLabels;
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
        => CollectAndExportAsTextAsync(to, ExpositionFormat.PrometheusText, cancel);

    /// <summary>
    /// Collects all metrics and exports them in text document format to the provided stream.
    /// 
    /// This method is designed to be used with custom output mechanisms that do not use an IMetricServer.
    /// </summary>
    public Task CollectAndExportAsTextAsync(Stream to, ExpositionFormat format, CancellationToken cancel = default)
    {
        if (to == null)
            throw new ArgumentNullException(nameof(to));

        return CollectAndSerializeAsync(new TextSerializer(to, format), cancel);
    }

    internal delegate TCollector CollectorInitializer<TCollector, TConfiguration>(string name, string help, in StringSequence instanceLabelNames, in LabelSequence staticLabels, TConfiguration configuration, ExemplarBehavior exemplarBehavior)
        where TCollector : Collector
        where TConfiguration : MetricConfiguration;

    /// <summary>
    /// Adds a collector to the registry, returning an existing instance if one with a matching name was already registered.
    /// </summary>
    internal TCollector GetOrAdd<TCollector, TConfiguration>(string name, string help, in StringSequence instanceLabelNames, in LabelSequence staticLabels, TConfiguration configuration, ExemplarBehavior exemplarBehavior, in CollectorInitializer<TCollector, TConfiguration> initializer)
        where TCollector : Collector
        where TConfiguration : MetricConfiguration
    {
        var family = GetOrAddCollectorFamily<TCollector>(name);

        var collectorIdentity = new CollectorIdentity(instanceLabelNames, staticLabels);

        return (TCollector)family.GetOrAdd(collectorIdentity, name, help, configuration, exemplarBehavior, initializer);
    }

    private CollectorFamily GetOrAddCollectorFamily<TCollector>(string finalName)
        where TCollector : Collector
    {
        static CollectorFamily ValidateFamily(CollectorFamily candidate)
        {
            // We either created a new collector family or found one with a matching identity.
            // We do some basic validation here to avoid silly API usage mistakes.

            if (candidate.CollectorType != typeof(TCollector))
                throw new InvalidOperationException("Collector of a different type with the same name is already registered.");

            return candidate;
        }

        // First try to get the family with only a read lock, with the assumption that it might already exist and therefore we do not need an expensive write lock.
        _familiesLock.EnterReadLock();

        try
        {
            if (_families.TryGetValue(finalName, out var existing))
                return ValidateFamily(existing);
        }
        finally
        {
            _familiesLock.ExitReadLock();
        }

        // It does not exist. OK, just create it.
        var newFamily = new CollectorFamily(typeof(TCollector));

        _familiesLock.EnterWriteLock();

        try
        {
#if NET
            // It could be that someone beats us to it! Probably not, though.
            if (_families.TryAdd(finalName, newFamily))
                return newFamily;

            return ValidateFamily(_families[finalName]);
#else
            // On .NET Fx we need to do the pessimistic case first because there is no TryAdd().
            if (_families.TryGetValue(finalName, out var existing))
                return ValidateFamily(existing);

            _families.Add(finalName, newFamily);
            return newFamily;
#endif
        }
        finally
        {
            _familiesLock.ExitWriteLock();
        }
    }

    // Each collector family has an identity (the base name of the metric, in Prometheus format) and any number of collectors within.
    // Different collectors in the same family may have different sets of labels (static and instance) depending on how they were created.
    private readonly Dictionary<string, CollectorFamily> _families = new(StringComparer.Ordinal);
    private readonly ReaderWriterLockSlim _familiesLock = new();

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
    private readonly object _firstCollectLock = new();

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

        await RunBeforeCollectCallbacksAsync(cancel);

        UpdateRegistryMetrics();

        // This could potentially take nontrivial time, as we are serializing to a stream (potentially, a network stream).
        // Therefore we operate on a defensive copy in a reused buffer.
        CollectorFamily[] buffer;

        _familiesLock.EnterReadLock();

        var familiesCount = _families.Count;
        buffer = ArrayPool<CollectorFamily>.Shared.Rent(familiesCount);

        try
        {
            try
            {
                _families.Values.CopyTo(buffer, 0);
            }
            finally
            {
                _familiesLock.ExitReadLock();
            }

            for (var i = 0; i < familiesCount; i++)
            {
                var family = buffer[i];
                await family.CollectAndSerializeAsync(serializer, cancel);
            }
        }
        finally
        {
            ArrayPool<CollectorFamily>.Shared.Return(buffer, clearArray: true);
        }

        await serializer.WriteEnd(cancel);
        await serializer.FlushAsync(cancel);
    }

    private async Task RunBeforeCollectCallbacksAsync(CancellationToken cancel)
    {
        foreach (var callback in _beforeCollectCallbacks)
        {
            try
            {
                callback();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Metrics before-collect callback failed: {ex}");
            }
        }

        await Task.WhenAll(_beforeCollectAsyncCallbacks.Select(async (callback) =>
        {
            try
            {
                await callback(cancel);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Metrics before-collect callback failed: {ex}");
            }
        }));
    }

    /// <summary>
    /// We collect some debug metrics from the registry itself to help indicate how many metrics we are publishing.
    /// </summary>
    internal void StartCollectingRegistryMetrics()
    {
        var factory = Metrics.WithCustomRegistry(this);

        _metricFamilies = factory.CreateGauge("prometheus_net_metric_families", "Number of metric families currently registered.", labelNames: [MetricTypeDebugLabel]);
        _metricInstances = factory.CreateGauge("prometheus_net_metric_instances", "Number of metric instances currently registered across all metric families.", labelNames: [MetricTypeDebugLabel]);
        _metricTimeseries = factory.CreateGauge("prometheus_net_metric_timeseries", "Number of metric timeseries currently generated from all metric instances.", labelNames: [MetricTypeDebugLabel]);

        _metricFamiliesPerType = [];
        _metricInstancesPerType = [];
        _metricTimeseriesPerType = [];

        foreach (MetricType type in Enum.GetValues(typeof(MetricType)))
        {
            var typeName = type.ToString().ToLowerInvariant();
            _metricFamiliesPerType[type] = _metricFamilies.WithLabels(typeName);
            _metricInstancesPerType[type] = _metricInstances.WithLabels(typeName);
            _metricTimeseriesPerType[type] = _metricTimeseries.WithLabels(typeName);
        }

        _startedCollectingRegistryMetrics.SetResult(true);
    }

    /// <summary>
    /// Registers a callback to be called when registry debug metrics are enabled.
    /// If the debug metrics have already been enabled, the callback is called immediately.
    /// </summary>
    internal void OnStartCollectingRegistryMetrics(Action callback)
    {
        _startedCollectingRegistryMetrics.Task.ContinueWith(delegate
        {
            callback();
            return Task.CompletedTask;
        });
    }

    private readonly TaskCompletionSource<object> _startedCollectingRegistryMetrics = new();

    private const string MetricTypeDebugLabel = "metric_type";

    private Gauge? _metricFamilies;
    private Gauge? _metricInstances;
    private Gauge? _metricTimeseries;

    private Dictionary<MetricType, Gauge.Child>? _metricFamiliesPerType;
    private Dictionary<MetricType, Gauge.Child>? _metricInstancesPerType;
    private Dictionary<MetricType, Gauge.Child>? _metricTimeseriesPerType;

    private void UpdateRegistryMetrics()
    {
        if (_metricFamiliesPerType == null || _metricInstancesPerType == null || _metricTimeseriesPerType == null)
            return; // Debug metrics are not enabled.

        // We copy references to the metric families to a temporary buffer to avoid having to hold locks to keep the collection consistent.
        CollectorFamily[] familiesBuffer;

        _familiesLock.EnterReadLock();

        var familiesCount = _families.Count;
        familiesBuffer = ArrayPool<CollectorFamily>.Shared.Rent(familiesCount);

        try
        {
            try
            {
                _families.Values.CopyTo(familiesBuffer, 0);
            }
            finally
            {
                _familiesLock.ExitReadLock();
            }

            foreach (MetricType type in Enum.GetValues(typeof(MetricType)))
            {
                long families = 0;
                long instances = 0;
                long timeseries = 0;

                for (var i = 0; i < familiesCount; i++)
                {
                    var family = familiesBuffer[i];

                    bool hadMatchingType = false;

                    family.ForEachCollector(collector =>
                    {
                        if (collector.Type != type)
                            return;

                        hadMatchingType = true;
                        instances += collector.ChildCount;
                        timeseries += collector.TimeseriesCount;
                    });

                    if (hadMatchingType)
                        families++;
                }

                _metricFamiliesPerType[type].Set(families);
                _metricInstancesPerType[type].Set(instances);
                _metricTimeseriesPerType[type].Set(timeseries);
            }
        }
        finally
        {
            ArrayPool<CollectorFamily>.Shared.Return(familiesBuffer, clearArray: true);
        }
    }

    // We only allow integration adapters to be started once per registry with the default configuration, to prevent double-counting values.
    // This is useful because we switched on adapters by default in 7.0.0 but if someone has manual .StartListening() calls from before, they would now count metrics double.
    internal bool PreventMeterAdapterRegistrationWithDefaultOptions;
    internal bool PreventEventCounterAdapterRegistrationWithDefaultOptions;
}
