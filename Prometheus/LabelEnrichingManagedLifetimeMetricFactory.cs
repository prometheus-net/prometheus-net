using System.Collections.Concurrent;

namespace Prometheus;

/// <summary>
/// Applies a set of static labels to lifetime-managed metrics. Multiple instances are functionally equivalent for the same label set.
/// </summary>
internal sealed class LabelEnrichingManagedLifetimeMetricFactory : IManagedLifetimeMetricFactory
{
    public LabelEnrichingManagedLifetimeMetricFactory(ManagedLifetimeMetricFactory inner, IDictionary<string, string> enrichWithLabels)
    {
        _inner = inner;

        // We just need the items to be consistently ordered between equivalent instances but it does not actually matter what the order is.
        _labels = enrichWithLabels.OrderBy(x => x.Key, StringComparer.Ordinal).ToList();

        _enrichWithLabelNames = enrichWithLabels.Select(x => x.Key).ToArray();
        _enrichWithLabelValues = enrichWithLabels.Select(x => x.Value).ToArray();
    }

    private readonly ManagedLifetimeMetricFactory _inner;

    // This is an ordered list because labels have specific order.
    private readonly IReadOnlyList<KeyValuePair<string, string>> _labels;

    // Cache the names/values to enrich with, for reuse.
    // We could perhaps improve even further via StringSequence but that requires creating separate internal APIs so can be a future optimization.
    private readonly string[] _enrichWithLabelNames;
    private readonly string[] _enrichWithLabelValues;

    public IManagedLifetimeMetricHandle<ICounter> CreateCounter(string name, string help, string[] labelNames, CounterConfiguration? configuration = null)
    {
        var combinedLabelNames = WithEnrichedLabelNames(labelNames);
        var innerHandle = _inner.CreateCounter(name, help, combinedLabelNames, configuration);

        // 1-1 relationship between instance of inner handle and our labeling handle.
        // We expect lifetime of each to match the lifetime of the respective factory, so no need to cleanup anything.
        return _counters.GetOrAdd(innerHandle, CreateCounterCore);
    }

    private LabelEnrichingManagedLifetimeCounter CreateCounterCore(IManagedLifetimeMetricHandle<ICounter> inner) => new LabelEnrichingManagedLifetimeCounter(inner, _enrichWithLabelValues);
    private readonly ConcurrentDictionary<IManagedLifetimeMetricHandle<ICounter>, LabelEnrichingManagedLifetimeCounter> _counters = new();

    public IManagedLifetimeMetricHandle<IGauge> CreateGauge(string name, string help, string[] labelNames, GaugeConfiguration? configuration = null)
    {
        var combinedLabelNames = WithEnrichedLabelNames(labelNames);
        var innerHandle = _inner.CreateGauge(name, help, combinedLabelNames, configuration);

        // 1-1 relationship between instance of inner handle and our labeling handle.
        // We expect lifetime of each to match the lifetime of the respective factory, so no need to cleanup anything.
        return _gauges.GetOrAdd(innerHandle, CreateGaugeCore);
    }

    private LabelEnrichingManagedLifetimeGauge CreateGaugeCore(IManagedLifetimeMetricHandle<IGauge> inner) => new LabelEnrichingManagedLifetimeGauge(inner, _enrichWithLabelValues);
    private readonly ConcurrentDictionary<IManagedLifetimeMetricHandle<IGauge>, LabelEnrichingManagedLifetimeGauge> _gauges = new();

    public IManagedLifetimeMetricHandle<IHistogram> CreateHistogram(string name, string help, string[] labelNames, HistogramConfiguration? configuration = null)
    {
        var combinedLabelNames = WithEnrichedLabelNames(labelNames);
        var innerHandle = _inner.CreateHistogram(name, help, combinedLabelNames, configuration);

        // 1-1 relationship between instance of inner handle and our labeling handle.
        // We expect lifetime of each to match the lifetime of the respective factory, so no need to cleanup anything.
        return _histograms.GetOrAdd(innerHandle, CreateHistogramCore);
    }

    private LabelEnrichingManagedLifetimeHistogram CreateHistogramCore(IManagedLifetimeMetricHandle<IHistogram> inner) => new LabelEnrichingManagedLifetimeHistogram(inner, _enrichWithLabelValues);
    private readonly ConcurrentDictionary<IManagedLifetimeMetricHandle<IHistogram>, LabelEnrichingManagedLifetimeHistogram> _histograms = new();

    public IManagedLifetimeMetricHandle<ISummary> CreateSummary(string name, string help, string[] labelNames, SummaryConfiguration? configuration = null)
    {
        var combinedLabelNames = WithEnrichedLabelNames(labelNames);
        var innerHandle = _inner.CreateSummary(name, help, combinedLabelNames, configuration);

        // 1-1 relationship between instance of inner handle and our labeling handle.
        // We expect lifetime of each to match the lifetime of the respective factory, so no need to cleanup anything.
        return _summaries.GetOrAdd(innerHandle, CreateSummaryCore);
    }

    private LabelEnrichingManagedLifetimeSummary CreateSummaryCore(IManagedLifetimeMetricHandle<ISummary> inner) => new LabelEnrichingManagedLifetimeSummary(inner, _enrichWithLabelValues);
    private readonly ConcurrentDictionary<IManagedLifetimeMetricHandle<ISummary>, LabelEnrichingManagedLifetimeSummary> _summaries = new();

    public IManagedLifetimeMetricFactory WithLabels(IDictionary<string, string> labels)
    {
        var combinedLabels = _labels.Concat(labels).ToDictionary(x => x.Key, x => x.Value);

        // Inner factory takes care of applying the correct ordering for labels.
        return _inner.WithLabels(combinedLabels);
    }

    private string[] WithEnrichedLabelNames(string[] instanceLabelNames)
    {
        // Enrichment labels always go first when we are communicating with the inner factory.
        return _enrichWithLabelNames.Concat(instanceLabelNames).ToArray();
    }
}
