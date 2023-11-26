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
internal sealed class ManagedLifetimeSummary : ManagedLifetimeMetricHandle<Summary.Child, ISummary>, ICollector<ISummary>
{
    static ManagedLifetimeSummary()
    {
        _assignUnlabelledFunc = AssignUnlabelled;
    }

    public ManagedLifetimeSummary(Collector<Summary.Child> metric, TimeSpan expiresAfter) : base(metric, expiresAfter)
    {
    }

    public override ICollector<ISummary> WithExtendLifetimeOnUse() => this;

    #region ICollector<ISummary> implementation (for WithExtendLifetimeOnUse)
    public string Name => _metric.Name;
    public string Help => _metric.Help;
    public string[] LabelNames => _metric.LabelNames;

    public ISummary Unlabelled => NonCapturingLazyInitializer.EnsureInitialized(ref _unlabelled, this, _assignUnlabelledFunc);
    private AutoLeasingInstance? _unlabelled;
    private static readonly Action<ManagedLifetimeSummary> _assignUnlabelledFunc;
    private static void AssignUnlabelled(ManagedLifetimeSummary instance) => instance._unlabelled = new AutoLeasingInstance(instance, Array.Empty<string>());

    public ISummary WithLabels(params string[] labelValues)
    {
        return new AutoLeasingInstance(this, labelValues);
    }
    #endregion

    private sealed class AutoLeasingInstance : ISummary
    {
        public AutoLeasingInstance(IManagedLifetimeMetricHandle<ISummary> inner, string[] labelValues)
        {
            _inner = inner;
            _labelValues = labelValues;
        }

        private readonly IManagedLifetimeMetricHandle<ISummary> _inner;
        private readonly string[] _labelValues;

        public void Observe(double val)
        {
            _inner.WithLease(x => x.Observe(val), _labelValues);
        }
    }
}
