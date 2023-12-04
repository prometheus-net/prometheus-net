namespace Prometheus;

internal sealed class ManagedLifetimeMetricFactory : IManagedLifetimeMetricFactory
{
    public ManagedLifetimeMetricFactory(MetricFactory inner, TimeSpan expiresAfter)
    {
        // .NET Framework requires the timer to fit in int.MaxValue and we will have hidden failures to expire if it does not.
        // For simplicity, let's just limit it to 1 day, which should be enough for anyone.
        if (expiresAfter > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(expiresAfter), "Automatic metric expiration time must be no greater than 1 day.");

        _inner = inner;
        _expiresAfter = expiresAfter;
    }

    private readonly MetricFactory _inner;
    private readonly TimeSpan _expiresAfter;

    public IManagedLifetimeMetricFactory WithLabels(IDictionary<string, string> labels)
    {
        return new LabelEnrichingManagedLifetimeMetricFactory(this, labels);
    }

    public IManagedLifetimeMetricHandle<ICounter> CreateCounter(string name, string help, string[]? instanceLabelNames, CounterConfiguration? configuration)
    {
        var identity = new ManagedLifetimeMetricIdentity(name, StringSequence.From(instanceLabelNames ?? Array.Empty<string>()));

        _countersLock.EnterReadLock();

        try
        {
            // Let's be optimistic and assume that in the typical case, the metric will already exist.
            if (_counters.TryGetValue(identity, out var existing))
                return existing;
        }
        finally
        {
            _countersLock.ExitReadLock();
        }

        var metric = _inner.CreateCounter(identity.MetricFamilyName, help, identity.InstanceLabelNames, configuration);
        var instance = new ManagedLifetimeCounter(metric, _expiresAfter);

        _countersLock.EnterWriteLock();

        try
        {
#if NET
            // It could be that someone beats us to it! Probably not, though.
            if (_counters.TryAdd(identity, instance))
                return instance;

            return _counters[identity];
#else
            // On .NET Fx we need to do the pessimistic case first because there is no TryAdd().
            if (_counters.TryGetValue(identity, out var existing))
                return existing;
            
            _counters.Add(identity, instance);
            return instance;
#endif
        }
        finally
        {
            _countersLock.ExitWriteLock();
        }
    }

    public IManagedLifetimeMetricHandle<IGauge> CreateGauge(string name, string help, string[]? instanceLabelNames, GaugeConfiguration? configuration)
    {
        var identity = new ManagedLifetimeMetricIdentity(name, StringSequence.From(instanceLabelNames ?? Array.Empty<string>()));

        _gaugesLock.EnterReadLock();

        try
        {
            // Let's be optimistic and assume that in the typical case, the metric will already exist.
            if (_gauges.TryGetValue(identity, out var existing))
                return existing;
        }
        finally
        {
            _gaugesLock.ExitReadLock();
        }

        var metric = _inner.CreateGauge(identity.MetricFamilyName, help, identity.InstanceLabelNames, configuration);
        var instance = new ManagedLifetimeGauge(metric, _expiresAfter);

        _gaugesLock.EnterWriteLock();

        try
        {
#if NET
            // It could be that someone beats us to it! Probably not, though.
            if (_gauges.TryAdd(identity, instance))
                return instance;

            return _gauges[identity];
#else
            // On .NET Fx we need to do the pessimistic case first because there is no TryAdd().
            if (_gauges.TryGetValue(identity, out var existing))
                return existing;
            
            _gauges.Add(identity, instance);
            return instance;
#endif
        }
        finally
        {
            _gaugesLock.ExitWriteLock();
        }
    }

    public IManagedLifetimeMetricHandle<IHistogram> CreateHistogram(string name, string help, string[]? instanceLabelNames, HistogramConfiguration? configuration)
    {
        var identity = new ManagedLifetimeMetricIdentity(name, StringSequence.From(instanceLabelNames ?? Array.Empty<string>()));

        _histogramsLock.EnterReadLock();

        try
        {
            // Let's be optimistic and assume that in the typical case, the metric will already exist.
            if (_histograms.TryGetValue(identity, out var existing))
                return existing;
        }
        finally
        {
            _histogramsLock.ExitReadLock();
        }

        var metric = _inner.CreateHistogram(identity.MetricFamilyName, help, identity.InstanceLabelNames, configuration);
        var instance = new ManagedLifetimeHistogram(metric, _expiresAfter);

        _histogramsLock.EnterWriteLock();

        try
        {
#if NET
            // It could be that someone beats us to it! Probably not, though.
            if (_histograms.TryAdd(identity, instance))
                return instance;

            return _histograms[identity];
#else
            // On .NET Fx we need to do the pessimistic case first because there is no TryAdd().
            if (_histograms.TryGetValue(identity, out var existing))
                return existing;
            
            _histograms.Add(identity, instance);
            return instance;
#endif
        }
        finally
        {
            _histogramsLock.ExitWriteLock();
        }
    }

    public IManagedLifetimeMetricHandle<ISummary> CreateSummary(string name, string help, string[]? instanceLabelNames, SummaryConfiguration? configuration)
    {
        var identity = new ManagedLifetimeMetricIdentity(name, StringSequence.From(instanceLabelNames ?? Array.Empty<string>()));

        _summariesLock.EnterReadLock();

        try
        {
            // Let's be optimistic and assume that in the typical case, the metric will already exist.
            if (_summaries.TryGetValue(identity, out var existing))
                return existing;
        }
        finally
        {
            _summariesLock.ExitReadLock();
        }

        var metric = _inner.CreateSummary(identity.MetricFamilyName, help, identity.InstanceLabelNames, configuration);
        var instance = new ManagedLifetimeSummary(metric, _expiresAfter);

        _summariesLock.EnterWriteLock();

        try
        {
#if NET
            // It could be that someone beats us to it! Probably not, though.
            if (_summaries.TryAdd(identity, instance))
                return instance;

            return _summaries[identity];
#else
            // On .NET Fx we need to do the pessimistic case first because there is no TryAdd().
            if (_summaries.TryGetValue(identity, out var existing))
                return existing;
            
            _summaries.Add(identity, instance);
            return instance;
#endif
        }
        finally
        {
            _summariesLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets all the existing label names predefined either in the factory or in the registry.
    /// </summary>
    internal StringSequence GetAllStaticLabelNames() => _inner.GetAllStaticLabelNames();

    // We need to reuse existing instances of lifetime-managed metrics because the user might not want to cache it.
    // This somewhat duplicates the metric identity tracking logic in CollectorRegistry but this is intentional, as we really do need to do this work on two layers.
    // We never remove collectors from here as long as the factory is alive. The expectation is that there is not an unbounded set of label names, so this set is non-gigantic.
    private readonly Dictionary<ManagedLifetimeMetricIdentity, ManagedLifetimeCounter> _counters = new();
    private readonly ReaderWriterLockSlim _countersLock = new();

    private readonly Dictionary<ManagedLifetimeMetricIdentity, ManagedLifetimeGauge> _gauges = new();
    private readonly ReaderWriterLockSlim _gaugesLock = new();

    private readonly Dictionary<ManagedLifetimeMetricIdentity, ManagedLifetimeHistogram> _histograms = new();
    private readonly ReaderWriterLockSlim _histogramsLock = new();

    private readonly Dictionary<ManagedLifetimeMetricIdentity, ManagedLifetimeSummary> _summaries = new();
    private readonly ReaderWriterLockSlim _summariesLock = new();
}
