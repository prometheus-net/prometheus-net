namespace Prometheus;

/// <summary>
/// This class implements two sets of functionality:
/// 1. A lifetime-managed metric handle that can be used to take leases on the metric.
/// 2. An automatically-lifetime-extending-on-use metric that creates leases automatically.
/// 
/// While conceptually separate, we merge the two sets into one class to avoid allocating a bunch of small objects
/// every time you want to obtain a lifetime-extending-on-use metric (which tends to be on a relatively hot path).
/// 
/// The lifetime-extending feature only supports write operations because we cannot guarantee that the metric is still alive when reading.
/// </summary>
internal sealed class ManagedLifetimeGauge : ManagedLifetimeMetricHandle<Gauge.Child, IGauge>, ICollector<IGauge>
{
    static ManagedLifetimeGauge()
    {
        _assignUnlabelledFunc = AssignUnlabelled;
    }

    public ManagedLifetimeGauge(Collector<Gauge.Child> metric, TimeSpan expiresAfter) : base(metric, expiresAfter)
    {
    }

    public override ICollector<IGauge> WithExtendLifetimeOnUse() => this;

    #region ICollector<IGauge> implementation (for WithExtendLifetimeOnUse)
    public string Name => _metric.Name;
    public string Help => _metric.Help;
    public string[] LabelNames => _metric.LabelNames;

    public IGauge Unlabelled => NonCapturingLazyInitializer.EnsureInitialized(ref _unlabelled, this, _assignUnlabelledFunc);
    private AutoLeasingInstance? _unlabelled;
    private static readonly Action<ManagedLifetimeGauge> _assignUnlabelledFunc;
    private static void AssignUnlabelled(ManagedLifetimeGauge instance) => instance._unlabelled = new AutoLeasingInstance(instance, Array.Empty<string>());

    // These do not get cached, so are potentially expensive - user code should try avoiding re-allocating these when possible,
    // though admittedly this may not be so easy as often these are on the hot path and the very reason that lifetime-managed
    // metrics are used is that we do not have a meaningful way to reuse metrics or identify their lifetime.
    public IGauge WithLabels(params string[] labelValues)
    {
        return new AutoLeasingInstance(this, labelValues);
    }
    #endregion

    private sealed class AutoLeasingInstance : IGauge
    {
        public AutoLeasingInstance(IManagedLifetimeMetricHandle<IGauge> inner, string[] labelValues)
        {
            _inner = inner;
            _labelValues = labelValues;
        }

        private readonly IManagedLifetimeMetricHandle<IGauge> _inner;
        private readonly string[] _labelValues;

        public double Value => throw new NotSupportedException("Read operations on a lifetime-extending-on-use expiring metric are not supported.");

        public void Inc(double increment = 1)
        {
            _inner.WithLease(x => x.Inc(increment), _labelValues);
        }

        public void Set(double val)
        {
            _inner.WithLease(x => x.Set(val), _labelValues);
        }

        public void Dec(double decrement = 1)
        {
            _inner.WithLease(x => x.Dec(decrement), _labelValues);
        }

        public void IncTo(double targetValue)
        {
            _inner.WithLease(x => x.IncTo(targetValue), _labelValues);
        }

        public void DecTo(double targetValue)
        {
            _inner.WithLease(x => x.DecTo(targetValue), _labelValues);
        }
    }
}
