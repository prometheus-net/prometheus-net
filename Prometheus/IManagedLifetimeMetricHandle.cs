namespace Prometheus;

/// <summary>
/// Handle to a metric with a lease-extended lifetime, enabling the metric to be accessed and its lifetime to be controlled.
/// Each label combination is automatically deleted N seconds after the last lease on that label combination expires.
/// </summary>
/// <remarks>
/// When creating leases, prefer the overload that takes a ReadOnlySpan because it avoids
/// allocating a string array if the metric instance you are leasing is already alive.
/// </remarks>
public interface IManagedLifetimeMetricHandle<TMetricInterface>
    where TMetricInterface : ICollectorChild
{
    #region Lease(string[])
    /// <summary>
    /// Takes a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    IDisposable AcquireLease(out TMetricInterface metric, params string[] labelValues);

    /// <summary>
    /// Takes a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// The lease is returned as a stack-only struct, which is faster than the IDisposable version.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    RefLease AcquireRefLease(out TMetricInterface metric, params string[] labelValues);

    /// <summary>
    /// While executing an action, holds a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    void WithLease(Action<TMetricInterface> action, params string[] labelValues);

    /// <summary>
    /// While executing an action, holds a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// Passes a given argument to the callback.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    void WithLease<TArg>(Action<TArg, TMetricInterface> action, TArg arg, params string[] labelValues);

    /// <summary>
    /// While executing an action, holds a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    Task WithLeaseAsync(Func<TMetricInterface, Task> func, params string[] labelValues);

    /// <summary>
    /// While executing a function, holds a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    TResult WithLease<TResult>(Func<TMetricInterface, TResult> func, params string[] labelValues);

    /// <summary>
    /// While executing a function, holds a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    Task<TResult> WithLeaseAsync<TResult>(Func<TMetricInterface, Task<TResult>> action, params string[] labelValues);
    #endregion

    #region Lease(ReadOnlyMemory<string>)
    /// <summary>
    /// Takes a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    IDisposable AcquireLease(out TMetricInterface metric, ReadOnlyMemory<string> labelValues);

    /// <summary>
    /// Takes a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// The lease is returned as a stack-only struct, which is faster than the IDisposable version.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    RefLease AcquireRefLease(out TMetricInterface metric, ReadOnlyMemory<string> labelValues);

    /// <summary>
    /// While executing an action, holds a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    void WithLease(Action<TMetricInterface> action, ReadOnlyMemory<string> labelValues);

    /// <summary>
    /// While executing an action, holds a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// Passes a given argument to the callback.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    void WithLease<TArg>(Action<TArg, TMetricInterface> action, TArg arg, ReadOnlyMemory<string> labelValues);

    /// <summary>
    /// While executing an action, holds a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    Task WithLeaseAsync(Func<TMetricInterface, Task> func, ReadOnlyMemory<string> labelValues);


    /// <summary>
    /// While executing a function, holds a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    TResult WithLease<TResult>(Func<TMetricInterface, TResult> func, ReadOnlyMemory<string> labelValues);

    /// <summary>
    /// While executing a function, holds a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    Task<TResult> WithLeaseAsync<TResult>(Func<TMetricInterface, Task<TResult>> action, ReadOnlyMemory<string> labelValues);
    #endregion

    #region Lease(ReadOnlySpan<string>)
    /// <summary>
    /// Takes a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    IDisposable AcquireLease(out TMetricInterface metric, ReadOnlySpan<string> labelValues);

    /// <summary>
    /// Takes a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// The lease is returned as a stack-only struct, which is faster than the IDisposable version.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    RefLease AcquireRefLease(out TMetricInterface metric, ReadOnlySpan<string> labelValues);

    /// <summary>
    /// While executing an action, holds a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    void WithLease(Action<TMetricInterface> action, ReadOnlySpan<string> labelValues);

    /// <summary>
    /// While executing an action, holds a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// Passes a given argument to the callback.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    void WithLease<TArg>(Action<TArg, TMetricInterface> action, TArg arg, ReadOnlySpan<string> labelValues);

    /// <summary>
    /// While executing a function, holds a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    TResult WithLease<TResult>(Func<TMetricInterface, TResult> func, ReadOnlySpan<string> labelValues);
    #endregion

    /// <summary>
    /// Returns a metric instance that automatically extends the lifetime of the timeseries whenever the value is changed.
    /// This is equivalent to taking a lease for every update to the value, and immediately releasing the lease.
    /// 
    /// This is useful if the caller is lifetime-management-agnostic code that is not aware of the possibility to extend metric lifetime via leases.
    /// Do not use this if you can use explicit leases instead, as this is considerably less efficient.
    /// </summary>
    ICollector<TMetricInterface> WithExtendLifetimeOnUse();
}
