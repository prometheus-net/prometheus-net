namespace Prometheus
{
    internal sealed class ManagedLifetimeMetricFactory : IManagedLifetimeMetricFactory
    {
        public ManagedLifetimeMetricFactory(IMetricFactory inner, TimeSpan expiresAfter)
        {
            _inner = inner;
            _expiresAfter = expiresAfter;
        }

        private readonly IMetricFactory _inner;
        private readonly TimeSpan _expiresAfter;

        public ILeasedLifetimeMetric<ICounter> CreateCounter(string name, string help, CounterConfiguration? configuration = null)
        {
            var metric = _inner.CreateCounter(name, help, configuration);
            return new LeasedLifetimeCounter(metric, _expiresAfter);
        }

        public ILeasedLifetimeMetric<IGauge> CreateGauge(string name, string help, GaugeConfiguration? configuration = null)
        {
            var metric = _inner.CreateGauge(name, help, configuration);
            return new LeasedLifetimeGauge(metric, _expiresAfter);
        }

        public ILeasedLifetimeMetric<IHistogram> CreateHistogram(string name, string help, HistogramConfiguration? configuration = null)
        {
            var metric = _inner.CreateHistogram(name, help, configuration);
            return new LeasedLifetimeHistogram(metric, _expiresAfter);
        }

        public ILeasedLifetimeMetric<ISummary> CreateSummary(string name, string help, SummaryConfiguration? configuration = null)
        {
            var metric = _inner.CreateSummary(name, help, configuration);
            return new LeasedLifetimeSummary(metric, _expiresAfter);
        }
    }
}
