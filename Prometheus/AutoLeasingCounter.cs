namespace Prometheus
{
    /// <summary>
    /// A counter that automatically extends the lifetime of a lease-extended metric whenever it is used.
    /// It only supports write operations because we cannot guarantee that the metric is still alive when reading.
    /// </summary>
    internal sealed class AutoLeasingCounter : ICollector<ICounter>
    {
        public AutoLeasingCounter(IManagedLifetimeMetricHandle<ICounter> inner, ICollector<ICounter> root)
        {
            _inner = inner;
            _root = root;
        }

        private readonly IManagedLifetimeMetricHandle<ICounter> _inner;
        private readonly ICollector<ICounter> _root;

        public string Name => _root.Name;
        public string Help => _root.Help;
        public string[] LabelNames => _root.LabelNames;

        public ICounter Unlabelled => new Instance(_inner, Array.Empty<string>());

        public ICounter WithLabels(params string[] labelValues)
        {
            return new Instance(_inner, labelValues);
        }

        private sealed class Instance : ICounter
        {
            public Instance(IManagedLifetimeMetricHandle<ICounter> inner, string[] labelValues)
            {
                _inner = inner;
                _labelValues = labelValues;
            }

            private readonly IManagedLifetimeMetricHandle<ICounter> _inner;
            private readonly string[] _labelValues;

            public double Value => throw new NotSupportedException("Read operations on a lifetime-extending-on-use expiring metric are not supported.");

            public void Inc(double increment = 1,  params Exemplar.LabelPair[] exemplar)
            {
                _inner.WithLease(x => x.Inc(increment, exemplar), _labelValues);
            }

            public void IncTo(double targetValue)
            {
                _inner.WithLease(x => x.IncTo(targetValue), _labelValues);
            }
        }
    }
}
