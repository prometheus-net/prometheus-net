namespace Prometheus;

internal sealed class LabelEnrichingManagedLifetimeCounter : IManagedLifetimeMetricHandle<ICounter>
{
    public LabelEnrichingManagedLifetimeCounter(IManagedLifetimeMetricHandle<ICounter> inner, string[] enrichWithLabelValues)
    {
        _inner = inner;
        _enrichWithLabelValues = enrichWithLabelValues;
    }

    // Internal for manipulation during testing.
    internal readonly IManagedLifetimeMetricHandle<ICounter> _inner;
    private readonly string[] _enrichWithLabelValues;

    public IDisposable AcquireLease(out ICounter metric, params string[] labelValues)
    {
        return _inner.AcquireLease(out metric, WithEnrichedLabelValues(labelValues));
    }

    public RefLease AcquireRefLease(out ICounter metric, params string[] labelValues)
    {
        return _inner.AcquireRefLease(out metric, WithEnrichedLabelValues(labelValues));
    }

    public ICollector<ICounter> WithExtendLifetimeOnUse()
    {
        return new LabelEnrichingAutoLeasingMetric<ICounter>(_inner.WithExtendLifetimeOnUse(), _enrichWithLabelValues);
    }

    public void WithLease(Action<ICounter> action, params string[] labelValues)
    {
        _inner.WithLease(action, WithEnrichedLabelValues(labelValues));
    }

    public void WithLease<TArg>(Action<TArg, ICounter> action, TArg arg, params string[] labelValues)
    {
        _inner.WithLease(action, arg, WithEnrichedLabelValues(labelValues));
    }

    public TResult WithLease<TResult>(Func<ICounter, TResult> func, params string[] labelValues)
    {
        return _inner.WithLease(func, WithEnrichedLabelValues(labelValues));
    }

    public Task WithLeaseAsync(Func<ICounter, Task> func, params string[] labelValues)
    {
        return _inner.WithLeaseAsync(func, WithEnrichedLabelValues(labelValues));
    }

    public Task<TResult> WithLeaseAsync<TResult>(Func<ICounter, Task<TResult>> action, params string[] labelValues)
    {
        return _inner.WithLeaseAsync(action, WithEnrichedLabelValues(labelValues));
    }

    private string[] WithEnrichedLabelValues(string[] instanceLabelValues)
    {
        return _enrichWithLabelValues.Concat(instanceLabelValues).ToArray();
    }
}
