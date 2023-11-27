namespace Prometheus;

internal sealed class LabelEnrichingManagedLifetimeSummary : IManagedLifetimeMetricHandle<ISummary>
{
    public LabelEnrichingManagedLifetimeSummary(IManagedLifetimeMetricHandle<ISummary> inner, string[] enrichWithLabelValues)
    {
        _inner = inner;
        _enrichWithLabelValues = enrichWithLabelValues;
    }

    private readonly IManagedLifetimeMetricHandle<ISummary> _inner;
    private readonly string[] _enrichWithLabelValues;

    public IDisposable AcquireLease(out ISummary metric, params string[] labelValues)
    {
        return _inner.AcquireLease(out metric, WithEnrichedLabelValues(labelValues));
    }

    public ICollector<ISummary> WithExtendLifetimeOnUse()
    {
        return new LabelEnrichingAutoLeasingMetric<ISummary>(_inner.WithExtendLifetimeOnUse(), _enrichWithLabelValues);
    }

    public void WithLease(Action<ISummary> action, params string[] labelValues)
    {
        _inner.WithLease(action, WithEnrichedLabelValues(labelValues));
    }

    public void WithLease<TArg>(Action<TArg, ISummary> action, TArg arg, params string[] labelValues)
    {
        _inner.WithLease(action, arg, WithEnrichedLabelValues(labelValues));
    }

    public TResult WithLease<TResult>(Func<ISummary, TResult> func, params string[] labelValues)
    {
        return _inner.WithLease(func, WithEnrichedLabelValues(labelValues));
    }

    public Task WithLeaseAsync(Func<ISummary, Task> func, params string[] labelValues)
    {
        return _inner.WithLeaseAsync(func, WithEnrichedLabelValues(labelValues));
    }

    public Task<TResult> WithLeaseAsync<TResult>(Func<ISummary, Task<TResult>> action, params string[] labelValues)
    {
        return _inner.WithLeaseAsync(action, WithEnrichedLabelValues(labelValues));
    }

    private string[] WithEnrichedLabelValues(string[] instanceLabelValues)
    {
        return _enrichWithLabelValues.Concat(instanceLabelValues).ToArray();
    }
}
