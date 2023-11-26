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
internal sealed class ManagedLifetimeCounter : ManagedLifetimeMetricHandle<Counter.Child, ICounter>, ICollector<ICounter>
{
    static ManagedLifetimeCounter()
    {
        _assignUnlabelledFunc = AssignUnlabelled;
    }

    public ManagedLifetimeCounter(Collector<Counter.Child> metric, TimeSpan expiresAfter) : base(metric, expiresAfter)
    {
    }

    public override ICollector<ICounter> WithExtendLifetimeOnUse() => this;

    #region ICollector<ICounter> implementation (for WithExtendLifetimeOnUse)
    public string Name => _metric.Name;
    public string Help => _metric.Help;
    public string[] LabelNames => _metric.LabelNames;

    public ICounter Unlabelled => NonCapturingLazyInitializer.EnsureInitialized(ref _unlabelled, this, _assignUnlabelledFunc);
    private AutoLeasingInstance? _unlabelled;
    private static readonly Action<ManagedLifetimeCounter> _assignUnlabelledFunc;
    private static void AssignUnlabelled(ManagedLifetimeCounter instance) => instance._unlabelled = new AutoLeasingInstance(instance, Array.Empty<string>());

    // These do not get cached, so are potentially expensive - user code should try avoiding re-allocating these when possible,
    // though admittedly this may not be so easy as often these are on the hot path and the very reason that lifetime-managed
    // metrics are used is that we do not have a meaningful way to reuse metrics or identify their lifetime.
    public ICounter WithLabels(params string[] labelValues)
    {
        return new AutoLeasingInstance(this, labelValues);
    }
    #endregion

    private sealed class AutoLeasingInstance : ICounter
    {
        public AutoLeasingInstance(IManagedLifetimeMetricHandle<ICounter> inner, string[] labelValues)
        {
            _inner = inner;
            _labelValues = labelValues;
        }

        private readonly IManagedLifetimeMetricHandle<ICounter> _inner;
        private readonly string[] _labelValues;

        public double Value => throw new NotSupportedException("Read operations on a lifetime-extending-on-use expiring metric are not supported.");

        public void Inc(double increment)
        {
            Inc(increment, null);
        }

        public void Inc(Exemplar? exemplar)
        {
            Inc(increment: 1, exemplar: exemplar);
        }

        public void Inc(double increment, Exemplar? exemplar)
        {
            _inner.WithLease(x => x.Inc(increment, exemplar), _labelValues);
        }

        public void IncTo(double targetValue)
        {
            _inner.WithLease(x => x.IncTo(targetValue), _labelValues);
        }
    }
}
