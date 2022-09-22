namespace Prometheus
{
    /// <summary>
    /// A gauge that automatically extends the lifetime of a lease-extended metric whenever it is used.
    /// </summary>
    internal sealed class AutoLeasingGauge : ICollector<IGauge>
    {
        public AutoLeasingGauge(IManagedLifetimeMetricHandle<IGauge> inner, ICollector<IGauge> root)
        {
            _inner = inner;
            _root = root;
        }

        private readonly IManagedLifetimeMetricHandle<IGauge> _inner;
        private readonly ICollector<IGauge> _root;

        public string Name => _root.Name;
        public string Help => _root.Help;
        public string[] LabelNames => _root.LabelNames;

        public IGauge Unlabelled => new Instance(_inner, _root.Unlabelled, Array.Empty<string>());

        public IGauge WithLabels(params string[] labelValues)
        {
            return new Instance(_inner, _root.WithLabels(labelValues), labelValues);
        }

        private sealed class Instance : IGauge
        {
            public Instance(IManagedLifetimeMetricHandle<IGauge> inner, IGauge root, string[] labelValues)
            {
                _inner = inner;
                _root = root;
                _labelValues = labelValues;
            }

            private readonly IManagedLifetimeMetricHandle<IGauge> _inner;
            private readonly IGauge _root;
            private readonly string[] _labelValues;

            public double Value
            {
                get
                {
                    // Read operations do not take a lease to extend lifetime.
                    return _root.Value;
                }
            }

            public void Inc(double increment = 1)
            {
                using var lease = _inner.AcquireLease(out var instance, _labelValues);
                instance.Inc(increment);
            }

            public void Set(double val)
            {
                using var lease = _inner.AcquireLease(out var instance, _labelValues);
                instance.Set(val);
            }

            public void Dec(double decrement = 1)
            {
                using var lease = _inner.AcquireLease(out var instance, _labelValues);
                instance.Dec(decrement);
            }

            public void IncTo(double targetValue)
            {
                using var lease = _inner.AcquireLease(out var instance, _labelValues);
                instance.IncTo(targetValue);
            }

            public void DecTo(double targetValue)
            {
                using var lease = _inner.AcquireLease(out var instance, _labelValues);
                instance.DecTo(targetValue);
            }
        }
    }
}
