namespace Prometheus
{
    /// <summary>
    /// A Summary that automatically extends the lifetime of a lease-extended metric whenever it is used.
    /// It only supports write operations because we cannot guarantee that the metric is still alive when reading.
    /// </summary>
    internal sealed class AutoLeasingSummary : ICollector<ISummary>
    {
        public AutoLeasingSummary(IManagedLifetimeMetricHandle<ISummary> inner, ICollector<ISummary> root)
        {
            _inner = inner;
            _root = root;
        }

        private readonly IManagedLifetimeMetricHandle<ISummary> _inner;
        private readonly ICollector<ISummary> _root;

        public string Name => _root.Name;
        public string Help => _root.Help;
        public string[] LabelNames => _root.LabelNames;

        public ISummary Unlabelled => new Instance(_inner, Array.Empty<string>());

        public ISummary WithLabels(params string[] labelValues)
        {
            return new Instance(_inner, labelValues);
        }

        private sealed class Instance : ISummary
        {
            public Instance(IManagedLifetimeMetricHandle<ISummary> inner, string[] labelValues)
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
}
