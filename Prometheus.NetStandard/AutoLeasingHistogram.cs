namespace Prometheus
{
    /// <summary>
    /// A histogram that automatically extends the lifetime of a lease-extended metric whenever it is used.
    /// </summary>
    internal sealed class AutoLeasingHistogram : ICollector<IHistogram>
    {
        public AutoLeasingHistogram(IManagedLifetimeMetricHandle<IHistogram> inner, ICollector<IHistogram> root)
        {
            _inner = inner;
            _root = root;
        }

        private readonly IManagedLifetimeMetricHandle<IHistogram> _inner;
        private readonly ICollector<IHistogram> _root;

        public string Name => _root.Name;
        public string Help => _root.Help;
        public string[] LabelNames => _root.LabelNames;

        public IHistogram Unlabelled => new Instance(_inner, _root.Unlabelled, Array.Empty<string>());

        public IHistogram WithLabels(params string[] labelValues)
        {
            return new Instance(_inner, _root.WithLabels(labelValues), labelValues);
        }

        private sealed class Instance : IHistogram
        {
            public Instance(IManagedLifetimeMetricHandle<IHistogram> inner, IHistogram root, string[] labelValues)
            {
                _inner = inner;
                _root = root;
                _labelValues = labelValues;
            }

            private readonly IManagedLifetimeMetricHandle<IHistogram> _inner;
            private readonly IHistogram _root;
            private readonly string[] _labelValues;

            public double Sum
            {
                get
                {
                    // Read operations do not take a lease to extend lifetime.
                    return _root.Sum;
                }
            }

            public long Count
            {
                get
                {
                    // Read operations do not take a lease to extend lifetime.
                    return _root.Count;
                }
            }

            public void Observe(double val, long count)
            {
                using var lease = _inner.AcquireLease(out var instance, _labelValues);
                instance.Observe(val, count);
            }

            public void Observe(double val)
            {
                using var lease = _inner.AcquireLease(out var instance, _labelValues);
                instance.Observe(val);
            }
        }
    }
}
