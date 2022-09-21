namespace Prometheus;

/// <summary>
/// A metric with a lease-extended lifetime.
/// Each label combination is automatically unpublished N seconds after the last lease on that label combination expires.
/// </summary>
public interface ILeasedLifetimeMetric<TMetricInterface>
    where TMetricInterface : ICollectorChild
{
    /// <summary>
    /// Takes a lifetime-extending lease on the metric, scoped to a specific combination of label values.
    /// 
    /// The typical pattern is that the metric value is only modified when the caller is holding a lease on the metric.
    /// Automatic unpublishing of the metric will not occur until all leases on the metric are disposed and the expiration duration elapses.
    /// </summary>
    /// <remarks>
    /// Acquiring a new lease after the metric has been unpublished will re-publish the metric without preserving the old value.
    /// Re-publishing may return a new instance of the metric (data collected via expired instances will not be published).
    /// </remarks>
    IDisposable AcquireLease(out TMetricInterface metric, params string[] labelValues);

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
