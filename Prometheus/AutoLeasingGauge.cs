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
