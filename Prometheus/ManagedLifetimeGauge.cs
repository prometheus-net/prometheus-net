﻿namespace Prometheus;

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
        static AutoLeasingInstance()
        {
            _incCoreFunc = IncCore;
            _incToCoreFunc = IncToCore;
            _setCoreFunc = SetCore;
            _decCoreFunc = DecCore;
            _decToCoreFunc = DecToCore;
        }

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
            var args = new IncArgs(increment);
            _inner.WithLease(_incCoreFunc, args, _labelValues);
        }

        private readonly struct IncArgs(double increment)
        {
            public readonly double Increment = increment;
        }

        private static void IncCore(IncArgs args, IGauge gauge) => gauge.Inc(args.Increment);
        private static readonly Action<IncArgs, IGauge> _incCoreFunc;

        public void Set(double val)
        {
            var args = new SetArgs(val);
            _inner.WithLease(_setCoreFunc, args, _labelValues);
        }

        private readonly struct SetArgs(double val)
        {
            public readonly double Val = val;
        }

        private static void SetCore(SetArgs args, IGauge gauge) => gauge.Set(args.Val);
        private static readonly Action<SetArgs, IGauge> _setCoreFunc;

        public void Dec(double decrement = 1)
        {
            var args = new DecArgs(decrement);
            _inner.WithLease(_decCoreFunc, args, _labelValues);
        }

        private readonly struct DecArgs(double decrement)
        {
            public readonly double Decrement = decrement;
        }

        private static void DecCore(DecArgs args, IGauge gauge) => gauge.Dec(args.Decrement);
        private static readonly Action<DecArgs, IGauge> _decCoreFunc;

        public void IncTo(double targetValue)
        {
            var args = new IncToArgs(targetValue);
            _inner.WithLease(_incToCoreFunc, args, _labelValues);
        }

        private readonly struct IncToArgs(double targetValue)
        {
            public readonly double TargetValue = targetValue;
        }

        private static void IncToCore(IncToArgs args, IGauge gauge) => gauge.IncTo(args.TargetValue);
        private static readonly Action<IncToArgs, IGauge> _incToCoreFunc;

        public void DecTo(double targetValue)
        {
            var args = new DecToArgs(targetValue);
            _inner.WithLease(_decToCoreFunc, args, _labelValues);
        }

        private readonly struct DecToArgs(double targetValue)
        {
            public readonly double TargetValue = targetValue;
        }

        private static void DecToCore(DecToArgs args, IGauge gauge) => gauge.DecTo(args.TargetValue);
        private static readonly Action<DecToArgs, IGauge> _decToCoreFunc;
    }
}
