namespace Prometheus;

/// <summary>
/// A histogram that automatically extends the lifetime of a lease-extended metric whenever it is used.
/// It only supports write operations because we cannot guarantee that the metric is still alive when reading.
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

    public IHistogram Unlabelled => new Instance(_inner, Array.Empty<string>());

    public IHistogram WithLabels(params string[] labelValues)
    {
        return new Instance(_inner, labelValues);
    }

    private sealed class Instance : IHistogram
    {
        public Instance(IManagedLifetimeMetricHandle<IHistogram> inner, string[] labelValues)
        {
            _inner = inner;
            _labelValues = labelValues;
        }

        private readonly IManagedLifetimeMetricHandle<IHistogram> _inner;
        private readonly string[] _labelValues;

        public double Sum => throw new NotSupportedException("Read operations on a lifetime-extending-on-use expiring metric are not supported.");
        public long Count => throw new NotSupportedException("Read operations on a lifetime-extending-on-use expiring metric are not supported.");

        public void Observe(double val, long count)
        {
            _inner.WithLease(x => x.Observe(val, count), _labelValues);
        }

        public void Observe(double val, Exemplar? exemplar)
        {
            _inner.WithLease(x => x.Observe(val, exemplar), _labelValues);
        }

        public void Observe(double val)
        {
            Observe(val, null);
        }
    }
}
