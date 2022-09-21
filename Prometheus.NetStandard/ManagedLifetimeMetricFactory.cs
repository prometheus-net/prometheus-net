namespace Prometheus
{
    internal sealed class ManagedLifetimeMetricFactory : IManagedLifetimeMetricFactory
    {
        public ManagedLifetimeMetricFactory(IMetricFactory inner, TimeSpan expiresAfter)
        {
            // .NET Framework requires the timer to fit in int.MaxValue and we will have hidden failures to expire if it does not.
            // For simplicity, let's just limit it to 1 day, which should be enough for anyone.
            if (expiresAfter > TimeSpan.FromDays(1))
                throw new ArgumentOutOfRangeException(nameof(expiresAfter), "Automatic metric expiration time must be no greater than 1 day.");

            _inner = inner;
            _expiresAfter = expiresAfter;
        }

        private readonly IMetricFactory _inner;
        private readonly TimeSpan _expiresAfter;

        public IManagedLifetimeMetricHandle<ICounter> CreateCounter(string name, string help, CounterConfiguration? configuration = null)
        {
            var metric = _inner.CreateCounter(name, help, configuration);
            return new ManagedLifetimeCounter(metric, _expiresAfter);
        }

        public IManagedLifetimeMetricHandle<IGauge> CreateGauge(string name, string help, GaugeConfiguration? configuration = null)
        {
            var metric = _inner.CreateGauge(name, help, configuration);
            return new ManagedLifetimeGauge(metric, _expiresAfter);
        }

        public IManagedLifetimeMetricHandle<IHistogram> CreateHistogram(string name, string help, HistogramConfiguration? configuration = null)
        {
            var metric = _inner.CreateHistogram(name, help, configuration);
            return new ManagedLifetimeHistogram(metric, _expiresAfter);
        }

        public IManagedLifetimeMetricHandle<ISummary> CreateSummary(string name, string help, SummaryConfiguration? configuration = null)
        {
            var metric = _inner.CreateSummary(name, help, configuration);
            return new ManaggedLifetimeSummary(metric, _expiresAfter);
        }
    }
}
