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
    public ICounter WithLabels(params string[] labelValues) => WithLabels(labelValues.AsMemory());

    public ICounter WithLabels(ReadOnlyMemory<string> labelValues)
    {
        return new AutoLeasingInstance(this, labelValues);
    }

    public ICounter WithLabels(ReadOnlySpan<string> labelValues)
    {
        // We are allocating a long-lived auto-leasing wrapper here, so there is no way we can just use the span directly.
        // We must copy it to a long-lived array. Another reason to avoid re-allocating these as much as possible.
        return new AutoLeasingInstance(this, labelValues.ToArray());
    }
    #endregion

    private sealed class AutoLeasingInstance : ICounter
    {
        public AutoLeasingInstance(IManagedLifetimeMetricHandle<ICounter> inner, ReadOnlyMemory<string> labelValues)
        {
            _inner = inner;
            _labelValues = labelValues;
        }

        private readonly IManagedLifetimeMetricHandle<ICounter> _inner;
        private readonly ReadOnlyMemory<string> _labelValues;

        public double Value => throw new NotSupportedException("Read operations on a lifetime-extending-on-use expiring metric are not supported.");

        public void Inc(double increment) => Inc(increment, null);
        public void Inc(Exemplar? exemplar) => Inc(increment: 1, exemplar: exemplar);

        public void Inc(double increment, Exemplar? exemplar)
        {
            var args = new IncArgs(increment, exemplar);

            // We use the Span overload to signal that we expect the label values to be known already.
            _inner.WithLease(_incCoreFunc, args, _labelValues.Span);
        }

        private readonly struct IncArgs(double increment, Exemplar? exemplar)
        {
            public readonly double Increment = increment;
            public readonly Exemplar? Exemplar = exemplar;
        }

        private static void IncCore(IncArgs args, ICounter counter) => counter.Inc(args.Increment, args.Exemplar);
        private static readonly Action<IncArgs, ICounter> _incCoreFunc = IncCore;

        public void IncTo(double targetValue)
        {
            var args = new IncToArgs(targetValue);

            // We use the Span overload to signal that we expect the label values to be known already.
            _inner.WithLease(_incToCoreFunc, args, _labelValues.Span);
        }

        private readonly struct IncToArgs(double targetValue)
        {
            public readonly double TargetValue = targetValue;
        }

        private static void IncToCore(IncToArgs args, ICounter counter) => counter.IncTo(args.TargetValue);
        private static readonly Action<IncToArgs, ICounter> _incToCoreFunc = IncToCore;
    }
}
