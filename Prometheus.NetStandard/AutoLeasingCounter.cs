namespace Prometheus
{
    /// <summary>
    /// A counter that automatically extends the lifetime of a lease-extended metric whenever it is used.
    /// </summary>
    internal sealed class AutoLeasingCounter : ICollector<ICounter>
    {
        public AutoLeasingCounter(ILeasedLifetimeMetric<ICounter> inner, ICollector<ICounter> root)
        {
            _inner = inner;
            _root = root;
        }

        private readonly ILeasedLifetimeMetric<ICounter> _inner;
        private readonly ICollector<ICounter> _root;

        public string Name => _root.Name;
        public string Help => _root.Help;
        public string[] LabelNames => _root.LabelNames;

        public ICounter Unlabelled => new Instance(_inner, _root.Unlabelled, Array.Empty<string>());

        public ICounter WithLabels(params string[] labelValues)
        {
            return new Instance(_inner, _root.WithLabels(labelValues), labelValues);
        }

        private sealed class Instance : ICounter
        {
            public Instance(ILeasedLifetimeMetric<ICounter> inner, ICounter root, string[] labelValues)
            {
                _inner = inner;
                _root = root;
                _labelValues = labelValues;
            }

            private readonly ILeasedLifetimeMetric<ICounter> _inner;
            private readonly ICounter _root;
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

            public void IncTo(double targetValue)
            {
                using var lease = _inner.AcquireLease(out var instance, _labelValues);
                instance.IncTo(targetValue);
            }
        }
    }
}
