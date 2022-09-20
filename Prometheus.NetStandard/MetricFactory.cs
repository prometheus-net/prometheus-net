namespace Prometheus
{
    /// <summary>
    /// Adds metrics to a registry.
    /// </summary>
    public sealed class MetricFactory : IMetricFactory
    {
        private readonly CollectorRegistry _registry;

        // If set, these labels will be applied to all created metrics, acting as additional static labels scoped to this factory.
        private readonly Labels? _factoryLabels;

        internal MetricFactory(CollectorRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        internal MetricFactory(CollectorRegistry registry, Labels? withLabels)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _factoryLabels = withLabels;
        }

        private Labels CreateStaticLabels(MetricConfiguration metricConfiguration)
        {
            return _registry.WhileReadingStaticLabels(registryLabels =>
            {
                var labels = Labels.Empty;

                if (metricConfiguration.StaticLabels != null)
                    labels = labels.Concat(new Labels(metricConfiguration.StaticLabels));

                if (_factoryLabels.HasValue)
                    labels = labels.Concat(_factoryLabels.Value);

                labels = labels.Concat(registryLabels);

                return labels;
            });
        }

        /// <summary>
        /// Counters only increase in value and reset to zero when the process restarts.
        /// </summary>
        public Counter CreateCounter(string name, string help, CounterConfiguration? configuration = null)
        {
            return _registry.GetOrAdd(new CollectorRegistry.CollectorInitializer<Counter, CounterConfiguration>(
                (n, h, config) => new Counter(n, h, config.LabelNames, CreateStaticLabels(config), config.SuppressInitialValue),
                name, help, configuration ?? CounterConfiguration.Default));
        }

        /// <summary>
        /// Gauges can have any numeric value and change arbitrarily.
        /// </summary>
        public Gauge CreateGauge(string name, string help, GaugeConfiguration? configuration = null)
        {
            return _registry.GetOrAdd(new CollectorRegistry.CollectorInitializer<Gauge, GaugeConfiguration>(
                (n, h, config) => new Gauge(n, h, config.LabelNames, CreateStaticLabels(config), config.SuppressInitialValue),
                name, help, configuration ?? GaugeConfiguration.Default));
        }

        /// <summary>
        /// Summaries track the trends in events over time (10 minutes by default).
        /// </summary>
        public Summary CreateSummary(string name, string help, SummaryConfiguration? configuration = null)
        {
            return _registry.GetOrAdd(new CollectorRegistry.CollectorInitializer<Summary, SummaryConfiguration>(
                (n, h, config) => new Summary(n, h, config.LabelNames, CreateStaticLabels(config), config.SuppressInitialValue, config.Objectives, config.MaxAge, config.AgeBuckets, config.BufferSize),
                name, help, configuration ?? SummaryConfiguration.Default));
        }

        /// <summary>
        /// Histograms track the size and number of events in buckets.
        /// </summary>
        public Histogram CreateHistogram(string name, string help, HistogramConfiguration? configuration = null)
        {
            return _registry.GetOrAdd(new CollectorRegistry.CollectorInitializer<Histogram, HistogramConfiguration>(
                (n, h, config) => new Histogram(n, h, config.LabelNames, CreateStaticLabels(config), config.SuppressInitialValue, config.Buckets),
                name, help, configuration ?? HistogramConfiguration.Default));
        }

        /// <summary>
        /// Counters only increase in value and reset to zero when the process restarts.
        /// </summary>
        public Counter CreateCounter(string name, string help, params string[] labelNames) =>
            CreateCounter(name, help, new CounterConfiguration
            {
                LabelNames = labelNames
            });

        /// <summary>
        /// Gauges can have any numeric value and change arbitrarily.
        /// </summary>
        public Gauge CreateGauge(string name, string help, params string[] labelNames) =>
            CreateGauge(name, help, new GaugeConfiguration
            {
                LabelNames = labelNames
            });

        /// <summary>
        /// Summaries track the trends in events over time (10 minutes by default).
        /// </summary>
        public Summary CreateSummary(string name, string help, params string[] labelNames) =>
            CreateSummary(name, help, new SummaryConfiguration
            {
                LabelNames = labelNames
            });

        /// <summary>
        /// Histograms track the size and number of events in buckets.
        /// </summary>
        public Histogram CreateHistogram(string name, string help, params string[] labelNames) =>
            CreateHistogram(name, help, new HistogramConfiguration
            {
                LabelNames = labelNames
            });

        public IMetricFactory WithLabels(IDictionary<string, string> labels)
        {
            if (labels.Count == 0)
                return this;

            var newLabels = new Labels(labels);

            // This may be a Nth-level labeling.
            var newFactoryLabels = _factoryLabels ?? Labels.Empty;

            // Add the current labels.
            newFactoryLabels = newFactoryLabels.Concat(newLabels);

            // Try to merge with the registry labels, just to detect conflicts early (we throw away the result).
            newFactoryLabels.Concat(new Labels(_registry.StaticLabels));

            return new MetricFactory(_registry, newFactoryLabels);
        }
    }
}