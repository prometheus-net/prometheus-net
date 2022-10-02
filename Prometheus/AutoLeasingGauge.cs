namespace Prometheus
{
    /// <summary>
    /// A gauge that automatically extends the lifetime of a lease-extended metric whenever it is used.
    /// It only supports write operations because we cannot guarantee that the metric is still alive when reading.
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

        public IGauge Unlabelled => new Instance(_inner, Array.Empty<string>());

        public IGauge WithLabels(params string[] labelValues)
        {
            return new Instance(_inner, labelValues);
        }

        private sealed class Instance : IGauge
        {
            public Instance(IManagedLifetimeMetricHandle<IGauge> inner, string[] labelValues)
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
}
