namespace Prometheus;

internal sealed class LabelEnrichingManagedLifetimeGauge : IManagedLifetimeMetricHandle<IGauge>
{
    public LabelEnrichingManagedLifetimeGauge(IManagedLifetimeMetricHandle<IGauge> inner, string[] enrichWithLabelValues)
    {
        _inner = inner;
        _enrichWithLabelValues = enrichWithLabelValues;
    }

    private readonly IManagedLifetimeMetricHandle<IGauge> _inner;
    private readonly string[] _enrichWithLabelValues;

    public IDisposable AcquireLease(out IGauge metric, params string[] labelValues)
    {
        return _inner.AcquireLease(out metric, WithEnrichedLabelValues(labelValues));
    }

    public ICollector<IGauge> WithExtendLifetimeOnUse()
    {
        return new LabelEnrichingAutoLeasingMetric<IGauge>(_inner.WithExtendLifetimeOnUse(), _enrichWithLabelValues);
    }

    public void WithLease(Action<IGauge> action, params string[] labelValues)
    {
        _inner.WithLease(action, WithEnrichedLabelValues(labelValues));
    }

    public TResult WithLease<TResult>(Func<IGauge, TResult> func, params string[] labelValues)
    {
        return _inner.WithLease(func, WithEnrichedLabelValues(labelValues));
    }

    public Task WithLeaseAsync(Func<IGauge, Task> func, params string[] labelValues)
    {
        return _inner.WithLeaseAsync(func, WithEnrichedLabelValues(labelValues));
    }

    public Task<TResult> WithLeaseAsync<TResult>(Func<IGauge, Task<TResult>> action, params string[] labelValues)
    {
        return _inner.WithLeaseAsync(action, WithEnrichedLabelValues(labelValues));
    }

    private string[] WithEnrichedLabelValues(string[] instanceLabelValues)
    {
        return _enrichWithLabelValues.Concat(instanceLabelValues).ToArray();
    }
}
