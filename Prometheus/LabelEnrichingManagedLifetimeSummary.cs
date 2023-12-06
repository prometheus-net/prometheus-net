using System.Buffers;

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

    public ICollector<ISummary> WithExtendLifetimeOnUse()
    {
        return new LabelEnrichingAutoLeasingMetric<ISummary>(_inner.WithExtendLifetimeOnUse(), _enrichWithLabelValues);
    }

    #region Lease(string[])
    public IDisposable AcquireLease(out ISummary metric, params string[] labelValues)
    {
        return _inner.AcquireLease(out metric, WithEnrichedLabelValues(labelValues));
    }

    public RefLease AcquireRefLease(out ISummary metric, params string[] labelValues)
    {
        return _inner.AcquireRefLease(out metric, WithEnrichedLabelValues(labelValues));
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
    #endregion

    #region Lease(ReadOnlyMemory<string>)
    public IDisposable AcquireLease(out ISummary metric, ReadOnlyMemory<string> labelValues)
    {
        return _inner.AcquireLease(out metric, WithEnrichedLabelValues(labelValues));
    }

    public RefLease AcquireRefLease(out ISummary metric, ReadOnlyMemory<string> labelValues)
    {
        return _inner.AcquireRefLease(out metric, WithEnrichedLabelValues(labelValues));
    }

    public void WithLease(Action<ISummary> action, ReadOnlyMemory<string> labelValues)
    {
        _inner.WithLease(action, WithEnrichedLabelValues(labelValues));
    }

    public void WithLease<TArg>(Action<TArg, ISummary> action, TArg arg, ReadOnlyMemory<string> labelValues)
    {
        _inner.WithLease(action, arg, WithEnrichedLabelValues(labelValues));
    }

    public TResult WithLease<TResult>(Func<ISummary, TResult> func, ReadOnlyMemory<string> labelValues)
    {
        return _inner.WithLease(func, WithEnrichedLabelValues(labelValues));
    }

    public Task WithLeaseAsync(Func<ISummary, Task> func, ReadOnlyMemory<string> labelValues)
    {
        return _inner.WithLeaseAsync(func, WithEnrichedLabelValues(labelValues));
    }

    public Task<TResult> WithLeaseAsync<TResult>(Func<ISummary, Task<TResult>> action, ReadOnlyMemory<string> labelValues)
    {
        return _inner.WithLeaseAsync(action, WithEnrichedLabelValues(labelValues));
    }
    #endregion

    private string[] WithEnrichedLabelValues(string[] instanceLabelValues)
    {
        var enriched = new string[_enrichWithLabelValues.Length + instanceLabelValues.Length];
        _enrichWithLabelValues.CopyTo(enriched, 0);
        instanceLabelValues.CopyTo(enriched, _enrichWithLabelValues.Length);

        return enriched;
    }

    private string[] WithEnrichedLabelValues(ReadOnlyMemory<string> instanceLabelValues)
    {
        var enriched = new string[_enrichWithLabelValues.Length + instanceLabelValues.Length];
        _enrichWithLabelValues.CopyTo(enriched, 0);
        instanceLabelValues.Span.CopyTo(enriched.AsSpan(_enrichWithLabelValues.Length));

        return enriched;
    }

    #region Lease(ReadOnlySpan<string>)
    public IDisposable AcquireLease(out ISummary metric, ReadOnlySpan<string> labelValues)
    {
        var buffer = RentBufferForEnrichedLabelValues(labelValues);

        try
        {
            var enrichedLabelValues = AssembleEnrichedLabelValues(labelValues, buffer);
            return _inner.AcquireLease(out metric, enrichedLabelValues);
        }
        finally
        {
            ArrayPool<string>.Shared.Return(buffer);
        }
    }

    public RefLease AcquireRefLease(out ISummary metric, ReadOnlySpan<string> labelValues)
    {
        var buffer = RentBufferForEnrichedLabelValues(labelValues);

        try
        {
            var enrichedLabelValues = AssembleEnrichedLabelValues(labelValues, buffer);
            return _inner.AcquireRefLease(out metric, enrichedLabelValues);
        }
        finally
        {
            ArrayPool<string>.Shared.Return(buffer);
        }
    }

    public void WithLease(Action<ISummary> action, ReadOnlySpan<string> labelValues)
    {
        var buffer = RentBufferForEnrichedLabelValues(labelValues);

        try
        {
            var enrichedLabelValues = AssembleEnrichedLabelValues(labelValues, buffer);
            _inner.WithLease(action, enrichedLabelValues);
        }
        finally
        {
            ArrayPool<string>.Shared.Return(buffer);
        }
    }

    public void WithLease<TArg>(Action<TArg, ISummary> action, TArg arg, ReadOnlySpan<string> labelValues)
    {
        var buffer = RentBufferForEnrichedLabelValues(labelValues);

        try
        {
            var enrichedLabelValues = AssembleEnrichedLabelValues(labelValues, buffer);
            _inner.WithLease(action, arg, enrichedLabelValues);
        }
        finally
        {
            ArrayPool<string>.Shared.Return(buffer);
        }
    }

    public TResult WithLease<TResult>(Func<ISummary, TResult> func, ReadOnlySpan<string> labelValues)
    {
        var buffer = RentBufferForEnrichedLabelValues(labelValues);

        try
        {
            var enrichedLabelValues = AssembleEnrichedLabelValues(labelValues, buffer);
            return _inner.WithLease(func, enrichedLabelValues);
        }
        finally
        {
            ArrayPool<string>.Shared.Return(buffer);
        }
    }
    #endregion

    private string[] RentBufferForEnrichedLabelValues(ReadOnlySpan<string> instanceLabelValues)
        => ArrayPool<string>.Shared.Rent(instanceLabelValues.Length + _enrichWithLabelValues.Length);

    private ReadOnlySpan<string> AssembleEnrichedLabelValues(ReadOnlySpan<string> instanceLabelValues, string[] buffer)
    {
        _enrichWithLabelValues.CopyTo(buffer, 0);
        instanceLabelValues.CopyTo(buffer.AsSpan(_enrichWithLabelValues.Length));

        return buffer.AsSpan(0, _enrichWithLabelValues.Length + instanceLabelValues.Length);
    }
}
