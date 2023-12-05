using System.Buffers;

namespace Prometheus;

internal sealed class LabelEnrichingAutoLeasingMetric<TMetric> : ICollector<TMetric>
    where TMetric : ICollectorChild
{
    public LabelEnrichingAutoLeasingMetric(ICollector<TMetric> inner, string[] enrichWithLabelValues)
    {
        _inner = inner;
        _enrichWithLabelValues = enrichWithLabelValues;
    }

    private readonly ICollector<TMetric> _inner;
    private readonly string[] _enrichWithLabelValues;

    public TMetric Unlabelled
    {
        get
        {
            // If we are not provided any custom label values, we can be pretty sure the label values are not going to change
            // between calls, so reuse a buffer to avoid allocations when passing the data to the inner instance.
            var buffer = ArrayPool<string>.Shared.Rent(_enrichWithLabelValues.Length);

            try
            {
                _enrichWithLabelValues.CopyTo(buffer, 0);
                var finalLabelValues = buffer.AsSpan(0, _enrichWithLabelValues.Length);

                return _inner.WithLabels(finalLabelValues);
            }
            finally
            {
                ArrayPool<string>.Shared.Return(buffer);
            }
        }
    }

    public string Name => _inner.Name;
    public string Help => _inner.Help;

    // We do not display the enriched labels, they are transparent - this is only the instance-specific label names.
    public string[] LabelNames => _inner.LabelNames;

    public TMetric WithLabels(params string[] labelValues)
    {
        // The caller passing us string[] does not signal that the allocation is not needed - in all likelihood it is.
        // However, we do not want to allocate two arrays here (because we need to concatenate as well) so instead we
        // use the reusable-buffer overload to avoid at least one of the allocations.

        return WithLabels(labelValues.AsSpan());
    }

    public TMetric WithLabels(ReadOnlyMemory<string> labelValues)
    {
        // The caller passing us ReadOnlyMemory does not signal that the allocation is not needed - in all likelihood it is.
        // However, we do not want to allocate two arrays here (because we need to concatenate as well) so instead we
        // use the reusable-buffer overload to avoid at least one of the allocations.

        return WithLabels(labelValues.Span);
    }

    public TMetric WithLabels(ReadOnlySpan<string> labelValues)
    {
        // The ReadOnlySpan overload suggests that the label values may already be known to the metric,
        // so we should strongly avoid allocating memory here. Thus we copy everything to a reusable buffer.
        var buffer = ArrayPool<string>.Shared.Rent(_enrichWithLabelValues.Length + labelValues.Length);

        try
        {
            _enrichWithLabelValues.CopyTo(buffer, 0);
            labelValues.CopyTo(buffer.AsSpan(_enrichWithLabelValues.Length));
            var finalLabelValues = buffer.AsSpan(0, _enrichWithLabelValues.Length + labelValues.Length);

            return _inner.WithLabels(finalLabelValues);
        }
        finally
        {
            ArrayPool<string>.Shared.Return(buffer);
        }
    }
}
