namespace Prometheus
{
    /// <summary>
    /// Adds metrics to a registry.
    /// </summary>
    public sealed class MetricFactory : IMetricFactory
    {
        private readonly CollectorRegistry _registry;

        // If set, these labels will be applied to all created metrics, acting as additional static labels scoped to this factory.
        // These are appended to the metric-specific static labels set at metric creation time.
        private readonly LabelSequence _factoryLabels;

        // Both the factory-defined and the registry-defined static labels.
        // TODO: We should validate that registry labels cannot be defined any more once we have already resolved this.
        private readonly Lazy<LabelSequence> _staticLabelsLazy;

        internal MetricFactory(CollectorRegistry registry) : this(registry, LabelSequence.Empty)
        {
        }

        internal MetricFactory(CollectorRegistry registry, LabelSequence withLabels)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _factoryLabels = withLabels;

            _staticLabelsLazy = new Lazy<LabelSequence>(ResolveStaticLabels);
        }

        private LabelSequence ResolveStaticLabels()
        {
            if (_factoryLabels.Length != 0)
                return _factoryLabels.Concat(_registry.GetStaticLabels());
            else
                return _registry.GetStaticLabels();
        }

        /// <summary>
        /// Counters only increase in value and reset to zero when the process restarts.
        /// </summary>
        public Counter CreateCounter(string name, string help, CounterConfiguration? configuration = null)
            => CreateCounter(name, help, configuration?.LabelNames ?? Array.Empty<string>(), configuration);

        /// <summary>
        /// Gauges can have any numeric value and change arbitrarily.
        /// </summary>
        public Gauge CreateGauge(string name, string help, GaugeConfiguration? configuration = null)
            => CreateGauge(name, help, configuration?.LabelNames ?? Array.Empty<string>(), configuration);

        /// <summary>
        /// Summaries track the trends in events over time (10 minutes by default).
        /// </summary>
        public Summary CreateSummary(string name, string help, SummaryConfiguration? configuration = null)
            => CreateSummary(name, help, configuration?.LabelNames ?? Array.Empty<string>(), configuration);

        /// <summary>
        /// Histograms track the size and number of events in buckets.
        /// </summary>
        public Histogram CreateHistogram(string name, string help, HistogramConfiguration? configuration = null)
            => CreateHistogram(name, help, configuration?.LabelNames ?? Array.Empty<string>(), configuration);

        /// <summary>
        /// Counters only increase in value and reset to zero when the process restarts.
        /// </summary>
        public Counter CreateCounter(string name, string help, string[] labelNames, CounterConfiguration? configuration = null)
            => CreateCounter(name, help, StringSequence.From(labelNames), configuration);

        /// <summary>
        /// Gauges can have any numeric value and change arbitrarily.
        /// </summary>
        public Gauge CreateGauge(string name, string help, string[] labelNames, GaugeConfiguration? configuration = null)
            => CreateGauge(name, help, StringSequence.From(labelNames), configuration);

        /// <summary>
        /// Summaries track the trends in events over time (10 minutes by default).
        /// </summary>
        public Histogram CreateHistogram(string name, string help, string[] labelNames, HistogramConfiguration? configuration = null)
            => CreateHistogram(name, help, StringSequence.From(labelNames), configuration);

        /// <summary>
        /// Histograms track the size and number of events in buckets.
        /// </summary>
        public Summary CreateSummary(string name, string help, string[] labelNames, SummaryConfiguration? configuration = null)
            => CreateSummary(name, help, StringSequence.From(labelNames), configuration);

        internal Counter CreateCounter(string name, string help, StringSequence instanceLabelNames, CounterConfiguration? configuration)
        {
            static Counter CreateInstance(string finalName, string finalHelp, StringSequence finalInstanceLabelNames, LabelSequence finalStaticLabels, CounterConfiguration finalConfiguration)
            {
                return new Counter(finalName, finalHelp, finalInstanceLabelNames, finalStaticLabels, finalConfiguration.SuppressInitialValue);
            }

            var initializer = new CollectorRegistry.CollectorInitializer<Counter, CounterConfiguration>(CreateInstance, name, help, instanceLabelNames, _staticLabelsLazy.Value, configuration ?? CounterConfiguration.Default);
            return _registry.GetOrAdd(initializer);
        }

        internal Gauge CreateGauge(string name, string help, StringSequence instanceLabelNames, GaugeConfiguration? configuration)
        {
            static Gauge CreateInstance(string finalName, string finalHelp, StringSequence finalInstanceLabelNames, LabelSequence finalStaticLabels, GaugeConfiguration finalConfiguration)
            {
                return new Gauge(finalName, finalHelp, finalInstanceLabelNames, finalStaticLabels, finalConfiguration.SuppressInitialValue);
            }

            var initializer = new CollectorRegistry.CollectorInitializer<Gauge, GaugeConfiguration>(CreateInstance, name, help, instanceLabelNames, _staticLabelsLazy.Value, configuration ?? GaugeConfiguration.Default);
            return _registry.GetOrAdd(initializer);
        }

        internal Histogram CreateHistogram(string name, string help, StringSequence instanceLabelNames, HistogramConfiguration? configuration)
        {
            static Histogram CreateInstance(string finalName, string finalHelp, StringSequence finalInstanceLabelNames, LabelSequence finalStaticLabels, HistogramConfiguration finalConfiguration)
            {
                return new Histogram(finalName, finalHelp, finalInstanceLabelNames, finalStaticLabels, finalConfiguration.SuppressInitialValue, finalConfiguration.Buckets);
            }

            var initializer = new CollectorRegistry.CollectorInitializer<Histogram, HistogramConfiguration>(CreateInstance, name, help, instanceLabelNames, _staticLabelsLazy.Value, configuration ?? HistogramConfiguration.Default);
            return _registry.GetOrAdd(initializer);
        }

        internal Summary CreateSummary(string name, string help, StringSequence instanceLabelNames, SummaryConfiguration? configuration)
        {
            static Summary CreateInstance(string finalName, string finalHelp, StringSequence finalInstanceLabelNames, LabelSequence finalStaticLabels, SummaryConfiguration finalConfiguration)
            {
                return new Summary(finalName, finalHelp, finalInstanceLabelNames, finalStaticLabels, finalConfiguration.SuppressInitialValue,
                    finalConfiguration.Objectives, finalConfiguration.MaxAge, finalConfiguration.AgeBuckets, finalConfiguration.BufferSize);
            }

            var initializer = new CollectorRegistry.CollectorInitializer<Summary, SummaryConfiguration>(CreateInstance, name, help, instanceLabelNames, _staticLabelsLazy.Value, configuration ?? SummaryConfiguration.Default);
            return _registry.GetOrAdd(initializer);
        }

        /// <summary>
        /// Counters only increase in value and reset to zero when the process restarts.
        /// </summary>
        public Counter CreateCounter(string name, string help, params string[] labelNames) => CreateCounter(name, help, labelNames, null);

        /// <summary>
        /// Gauges can have any numeric value and change arbitrarily.
        /// </summary>
        public Gauge CreateGauge(string name, string help, params string[] labelNames) => CreateGauge(name, help, labelNames, null);

        /// <summary>
        /// Summaries track the trends in events over time (10 minutes by default).
        /// </summary>
        public Summary CreateSummary(string name, string help, params string[] labelNames) => CreateSummary(name, help, labelNames, null);

        /// <summary>
        /// Histograms track the size and number of events in buckets.
        /// </summary>
        public Histogram CreateHistogram(string name, string help, params string[] labelNames) => CreateHistogram(name, help, labelNames, null);

        public IMetricFactory WithLabels(IDictionary<string, string> labels)
        {
            if (labels.Count == 0)
                return this;

            var newLabels = LabelSequence.From(labels);

            // Add any already-inherited labels to the end (rule is that lower levels go first, higher levels last).
            var newFactoryLabels = newLabels.Concat(_factoryLabels);

            return new MetricFactory(_registry, newFactoryLabels);
        }

        /// <summary>
        /// Gets all the existing label names predefined either in the factory or in the registry.
        /// </summary>
        internal StringSequence GetAllStaticLabelNames()
        {
            return _factoryLabels.Names.Concat(_registry.GetStaticLabels().Names);
        }

        public IManagedLifetimeMetricFactory WithManagedLifetime(TimeSpan expiresAfter) =>
            new ManagedLifetimeMetricFactory(this, expiresAfter);
    }
}