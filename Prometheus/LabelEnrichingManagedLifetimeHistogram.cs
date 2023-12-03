using System.Buffers;

namespace Prometheus;

internal sealed class LabelEnrichingManagedLifetimeHistogram : IManagedLifetimeMetricHandle<IHistogram>
{
    public LabelEnrichingManagedLifetimeHistogram(IManagedLifetimeMetricHandle<IHistogram> inner, string[] enrichWithLabelValues)
    {
        _inner = inner;
        _enrichWithLabelValues = enrichWithLabelValues;
    }

    private readonly IManagedLifetimeMetricHandle<IHistogram> _inner;
    private readonly string[] _enrichWithLabelValues;

    public ICollector<IHistogram> WithExtendLifetimeOnUse()
    {
        return new LabelEnrichingAutoLeasingMetric<IHistogram>(_inner.WithExtendLifetimeOnUse(), _enrichWithLabelValues);
    }

    #region Lease(string[])
    public IDisposable AcquireLease(out IHistogram metric, params string[] labelValues)
    {
        return _inner.AcquireLease(out metric, WithEnrichedLabelValues(labelValues));
    }

    public RefLease AcquireRefLease(out IHistogram metric, params string[] labelValues)
    {
        return _inner.AcquireRefLease(out metric, WithEnrichedLabelValues(labelValues));
    }

    public void WithLease(Action<IHistogram> action, params string[] labelValues)
    {
        _inner.WithLease(action, WithEnrichedLabelValues(labelValues));
    }

    public void WithLease<TArg>(Action<TArg, IHistogram> action, TArg arg, params string[] labelValues)
    {
        _inner.WithLease(action, arg, WithEnrichedLabelValues(labelValues));
    }

    public TResult WithLease<TResult>(Func<IHistogram, TResult> func, params string[] labelValues)
    {
        return _inner.WithLease(func, WithEnrichedLabelValues(labelValues));
    }

    public Task WithLeaseAsync(Func<IHistogram, Task> func, params string[] labelValues)
    {
        return _inner.WithLeaseAsync(func, WithEnrichedLabelValues(labelValues));
    }

    public Task<TResult> WithLeaseAsync<TResult>(Func<IHistogram, Task<TResult>> action, params string[] labelValues)
    {
        return _inner.WithLeaseAsync(action, WithEnrichedLabelValues(labelValues));
    }
    #endregion

    #region Lease(ReadOnlyMemory<string>)
    public IDisposable AcquireLease(out IHistogram metric, ReadOnlyMemory<string> labelValues)
    {
        return _inner.AcquireLease(out metric, WithEnrichedLabelValues(labelValues));
    }

    public RefLease AcquireRefLease(out IHistogram metric, ReadOnlyMemory<string> labelValues)
    {
        return _inner.AcquireRefLease(out metric, WithEnrichedLabelValues(labelValues));
    }

    public void WithLease(Action<IHistogram> action, ReadOnlyMemory<string> labelValues)
    {
        _inner.WithLease(action, WithEnrichedLabelValues(labelValues));
    }

    public void WithLease<TArg>(Action<TArg, IHistogram> action, TArg arg, ReadOnlyMemory<string> labelValues)
    {
        _inner.WithLease(action, arg, WithEnrichedLabelValues(labelValues));
    }

    public TResult WithLease<TResult>(Func<IHistogram, TResult> func, ReadOnlyMemory<string> labelValues)
    {
        return _inner.WithLease(func, WithEnrichedLabelValues(labelValues));
    }

    public Task WithLeaseAsync(Func<IHistogram, Task> func, ReadOnlyMemory<string> labelValues)
    {
        return _inner.WithLeaseAsync(func, WithEnrichedLabelValues(labelValues));
    }

    public Task<TResult> WithLeaseAsync<TResult>(Func<IHistogram, Task<TResult>> action, ReadOnlyMemory<string> labelValues)
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
    public IDisposable AcquireLease(out IHistogram metric, ReadOnlySpan<string> labelValues)
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

    public RefLease AcquireRefLease(out IHistogram metric, ReadOnlySpan<string> labelValues)
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

    public void WithLease(Action<IHistogram> action, ReadOnlySpan<string> labelValues)
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

    public void WithLease<TArg>(Action<TArg, IHistogram> action, TArg arg, ReadOnlySpan<string> labelValues)
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

    public TResult WithLease<TResult>(Func<IHistogram, TResult> func, ReadOnlySpan<string> labelValues)
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
