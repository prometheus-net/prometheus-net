namespace Prometheus;

internal sealed class LabelEnrichingAutoLeasingMetric<TMetric> : ICollector<TMetric>
    where TMetric : ICollectorChild
{
    public LabelEnrichingAutoLeasingMetric(ICollector<TMetric> inner, string[] enrichWithLabelValues)
    {
        _inner = inner;
        _enrichedLabelValues = enrichWithLabelValues;
    }

    private readonly ICollector<TMetric> _inner;
    private readonly string[] _enrichedLabelValues;

    public TMetric Unlabelled => _inner.Unlabelled;
    public string Name => _inner.Name;
    public string Help => _inner.Help;

    // We do not display the enriched labels, they are transparent - this is only the instance-specific label names.
    public string[] LabelNames => _inner.LabelNames;

    public TMetric WithLabels(params string[] labelValues) => _inner.WithLabels(_enrichedLabelValues.Concat(labelValues).ToArray());
}
