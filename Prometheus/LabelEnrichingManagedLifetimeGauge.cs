using System.Buffers;

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

    #region Lease(string[])
    public IDisposable AcquireLease(out IGauge metric, params string[] labelValues)
    {
        return _inner.AcquireLease(out metric, WithEnrichedLabelValues(labelValues));
    }

    public RefLease AcquireRefLease(out IGauge metric, params string[] labelValues)
    {
        return _inner.AcquireRefLease(out metric, WithEnrichedLabelValues(labelValues));
    }

    public ICollector<IGauge> WithExtendLifetimeOnUse()
    {
        return new LabelEnrichingAutoLeasingMetric<IGauge>(_inner.WithExtendLifetimeOnUse(), _enrichWithLabelValues);
    }

    public void WithLease(Action<IGauge> action, params string[] labelValues)
    {
        _inner.WithLease(action, WithEnrichedLabelValues(labelValues));
    }

    public void WithLease<TArg>(Action<TArg, IGauge> action, TArg arg, params string[] labelValues)
    {
        _inner.WithLease(action, arg, WithEnrichedLabelValues(labelValues));
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
    #endregion

    private string[] WithEnrichedLabelValues(string[] instanceLabelValues)
    {
        return _enrichWithLabelValues.Concat(instanceLabelValues).ToArray();
    }

    #region Lease(ReadOnlySpan<string>)
    public IDisposable AcquireLease(out IGauge metric, ReadOnlySpan<string> labelValues)
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

    public RefLease AcquireRefLease(out IGauge metric, ReadOnlySpan<string> labelValues)
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

    public void WithLease(Action<IGauge> action, ReadOnlySpan<string> labelValues)
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

    public void WithLease<TArg>(Action<TArg, IGauge> action, TArg arg, ReadOnlySpan<string> labelValues)
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

    public TResult WithLease<TResult>(Func<IGauge, TResult> func, ReadOnlySpan<string> labelValues)
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
