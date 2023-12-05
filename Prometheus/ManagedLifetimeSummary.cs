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

    // These do not get cached, so are potentially expensive - user code should try avoiding re-allocating these when possible,
    // though admittedly this may not be so easy as often these are on the hot path and the very reason that lifetime-managed
    // metrics are used is that we do not have a meaningful way to reuse metrics or identify their lifetime.
    public ISummary WithLabels(params string[] labelValues) => WithLabels(labelValues.AsMemory());

    public ISummary WithLabels(ReadOnlyMemory<string> labelValues)
    {
        return new AutoLeasingInstance(this, labelValues);
    }

    public ISummary WithLabels(ReadOnlySpan<string> labelValues)
    {
        // We are allocating a long-lived auto-leasing wrapper here, so there is no way we can just use the span directly.
        // We must copy it to a long-lived array. Another reason to avoid re-allocating these as much as possible.
        return new AutoLeasingInstance(this, labelValues.ToArray());
    }
    #endregion

    private sealed class AutoLeasingInstance : ISummary
    {
        public AutoLeasingInstance(IManagedLifetimeMetricHandle<ISummary> inner, ReadOnlyMemory<string> labelValues)
        {
            _inner = inner;
            _labelValues = labelValues;
        }

        private readonly IManagedLifetimeMetricHandle<ISummary> _inner;
        private readonly ReadOnlyMemory<string> _labelValues;

        public void Observe(double val)
        {
            var args = new ObserveArgs(val);

            // We use the Span overload to signal that we expect the label values to be known already.
            _inner.WithLease(_observeCoreFunc, args, _labelValues.Span);
        }

        private readonly struct ObserveArgs(double val)
        {
            public readonly double Val = val;
        }

        private static void ObserveCore(ObserveArgs args, ISummary summary) => summary.Observe(args.Val);
        private static readonly Action<ObserveArgs, ISummary> _observeCoreFunc = ObserveCore;
    }
}
