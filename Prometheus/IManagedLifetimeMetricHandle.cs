namespace Prometheus;

/// <summary>
/// Handle to a metric with a lease-extended lifetime, enabling the metric to be accessed and its lifetime to be controlled.
/// Each label combination is automatically unpublished N seconds after the last lease on that label combination expires.
/// </summary>
public interface IManagedLifetimeMetricHandle<TMetricInterface>
    where TMetricInterface : ICollectorChild
{
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
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic removal of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been removed will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    ValueTask WithLeaseAsync(Func<TMetricInterface, ValueTask> func, params string[] labelValues);

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
    ValueTask<TResult> WithLeaseAsync<TResult>(Func<TMetricInterface, ValueTask<TResult>> action, params string[] labelValues);

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

    /// <summary>
    /// Returns a metric instance that automatically extends the lifetime of the timeseries whenever the value is changed.
    /// This is equivalent to taking a lease for every update to the value, and immediately releasing the lease.
    /// 
    /// This is useful if:
    /// 1) the caller does not perform any long-running operations that would require keeping a lease for more than 1 update;
    /// 2) or if the caller is lifetime-management-agnostic code that is not aware of the possibility to extend metric lifetime via leases.
    /// </summary>
    ICollector<TMetricInterface> WithExtendLifetimeOnUse();
}
