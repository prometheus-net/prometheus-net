using System.Collections.Concurrent;

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

    public IManagedLifetimeMetricHandle<ICounter> CreateCounter(string name, string help, string[] instanceLabelNames, CounterConfiguration? configuration = null)
    {
        var identity = new ManagedLifetimeMetricIdentity(name, StringSequence.From(instanceLabelNames));

        // Let's be optimistic and assume that in the typical case, the metric will already exist.
        if (_counters.TryGetValue(identity, out var existing))
            return existing;

        var initializer = new CounterInitializer(_inner, _expiresAfter, help, configuration);
        return _counters.GetOrAdd(identity, initializer.CreateInstance);
    }

    public IManagedLifetimeMetricHandle<IGauge> CreateGauge(string name, string help, string[] instanceLabelNames, GaugeConfiguration? configuration = null)
    {
        var identity = new ManagedLifetimeMetricIdentity(name, StringSequence.From(instanceLabelNames));

        // Let's be optimistic and assume that in the typical case, the metric will already exist.
        if (_gauges.TryGetValue(identity, out var existing))
            return existing;

        var initializer = new GaugeInitializer(_inner, _expiresAfter, help, configuration);
        return _gauges.GetOrAdd(identity, initializer.CreateInstance);
    }

    public IManagedLifetimeMetricHandle<IHistogram> CreateHistogram(string name, string help, string[] instanceLabelNames, HistogramConfiguration? configuration = null)
    {
        var identity = new ManagedLifetimeMetricIdentity(name, StringSequence.From(instanceLabelNames));

        // Let's be optimistic and assume that in the typical case, the metric will already exist.
        if (_histograms.TryGetValue(identity, out var existing))
            return existing;

        var initializer = new HistogramInitializer(_inner, _expiresAfter, help, configuration);
        return _histograms.GetOrAdd(identity, initializer.CreateInstance);
    }

    public IManagedLifetimeMetricHandle<ISummary> CreateSummary(string name, string help, string[] instanceLabelNames, SummaryConfiguration? configuration = null)
    {
        var identity = new ManagedLifetimeMetricIdentity(name, StringSequence.From(instanceLabelNames));

        // Let's be optimistic and assume that in the typical case, the metric will already exist.
        if (_summaries.TryGetValue(identity, out var existing))
            return existing;

        var initializer = new SummaryInitializer(_inner, _expiresAfter, help, configuration);
        return _summaries.GetOrAdd(identity, initializer.CreateInstance);
    }

    /// <summary>
    /// Gets all the existing label names predefined either in the factory or in the registry.
    /// </summary>
    internal StringSequence GetAllStaticLabelNames() => _inner.GetAllStaticLabelNames();

    // We need to reuse existing instances of lifetime-managed metrics because the user might not want to cache it.
    // This somewhat duplicates the metric identity tracking logic in CollectorRegistry but this is intentional, as we really do need to do this work on two layers.
    // We never remove collectors from here as long as the factory is alive. The expectation is that there is not an unbounded set of label names, so this set is non-gigantic.
    private readonly ConcurrentDictionary<ManagedLifetimeMetricIdentity, ManagedLifetimeCounter> _counters = new();
    private readonly ConcurrentDictionary<ManagedLifetimeMetricIdentity, ManagedLifetimeGauge> _gauges = new();
    private readonly ConcurrentDictionary<ManagedLifetimeMetricIdentity, ManagedLifetimeHistogram> _histograms = new();
    private readonly ConcurrentDictionary<ManagedLifetimeMetricIdentity, ManagedLifetimeSummary> _summaries = new();

    private readonly struct CounterInitializer
    {
        public readonly MetricFactory Inner;
        public readonly TimeSpan ExpiresAfter;
        public readonly string Help;
        public readonly CounterConfiguration? Configuration;

        public CounterInitializer(MetricFactory inner, TimeSpan expiresAfter, string help,  CounterConfiguration? configuration)
        {
            Inner = inner;
            ExpiresAfter = expiresAfter;
            Help = help;
            Configuration = configuration;
        }

        public ManagedLifetimeCounter CreateInstance(ManagedLifetimeMetricIdentity identity)
        {
            var metric = Inner.CreateCounter(identity.MetricFamilyName, Help, identity.InstanceLabelNames, Configuration);
            return new ManagedLifetimeCounter(metric, ExpiresAfter);
        }
    }

    private readonly struct GaugeInitializer
    {
        public readonly MetricFactory Inner;
        public readonly TimeSpan ExpiresAfter;
        public readonly string Help;
        public readonly GaugeConfiguration? Configuration;

        public GaugeInitializer(MetricFactory inner, TimeSpan expiresAfter, string help, GaugeConfiguration? configuration)
        {
            Inner = inner;
            ExpiresAfter = expiresAfter;
            Help = help;
            Configuration = configuration;
        }

        public ManagedLifetimeGauge CreateInstance(ManagedLifetimeMetricIdentity identity)
        {
            var metric = Inner.CreateGauge(identity.MetricFamilyName, Help, identity.InstanceLabelNames, Configuration);
            return new ManagedLifetimeGauge(metric, ExpiresAfter);
        }
    }

    private readonly struct HistogramInitializer
    {
        public readonly MetricFactory Inner;
        public readonly TimeSpan ExpiresAfter;
        public readonly string Help;
        public readonly HistogramConfiguration? Configuration;

        public HistogramInitializer(MetricFactory inner, TimeSpan expiresAfter, string help, HistogramConfiguration? configuration)
        {
            Inner = inner;
            ExpiresAfter = expiresAfter;
            Help = help;
            Configuration = configuration;
        }

        public ManagedLifetimeHistogram CreateInstance(ManagedLifetimeMetricIdentity identity)
        {
            var metric = Inner.CreateHistogram(identity.MetricFamilyName, Help, identity.InstanceLabelNames, Configuration);
            return new ManagedLifetimeHistogram(metric, ExpiresAfter);
        }
    }

    private readonly struct SummaryInitializer
    {
        public readonly MetricFactory Inner;
        public readonly TimeSpan ExpiresAfter;
        public readonly string Help;
        public readonly SummaryConfiguration? Configuration;

        public SummaryInitializer(MetricFactory inner, TimeSpan expiresAfter, string help, SummaryConfiguration? configuration)
        {
            Inner = inner;
            ExpiresAfter = expiresAfter;
            Help = help;
            Configuration = configuration;
        }

        public ManagedLifetimeSummary CreateInstance(ManagedLifetimeMetricIdentity identity)
        {
            var metric = Inner.CreateSummary(identity.MetricFamilyName, Help, identity.InstanceLabelNames, Configuration);
            return new ManagedLifetimeSummary(metric, ExpiresAfter);
        }
    }
}
