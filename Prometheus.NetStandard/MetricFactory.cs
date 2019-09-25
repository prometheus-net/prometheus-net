using System;

namespace Prometheus
{
    /// <summary>
    /// Adds metrics to a registry.
    /// </summary>
    public sealed class MetricFactory
    {
        private readonly CollectorRegistry _registry;

        internal MetricFactory(CollectorRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// Counters only increase in value and reset to zero when the process restarts.
        /// </summary>
        public Counter CreateCounter(string name, string help, CounterConfiguration? configuration = null)
        {
            return _registry.GetOrAdd(new CollectorRegistry.CollectorInitializer<Counter, CounterConfiguration>(
                (n, h, config) => new Counter(n, h, config.LabelNames, config.SuppressInitialValue),
                name, help, configuration ?? CounterConfiguration.Default));
        }

        /// <summary>
        /// Gauges can have any numeric value and change arbitrarily.
        /// </summary>
        public Gauge CreateGauge(string name, string help, GaugeConfiguration? configuration = null)
        {
            return _registry.GetOrAdd(new CollectorRegistry.CollectorInitializer<Gauge, GaugeConfiguration>(
                (n, h, config) => new Gauge(n, h, config.LabelNames, config.SuppressInitialValue),
                name, help, configuration ?? GaugeConfiguration.Default));
        }

        /// <summary>
        /// Summaries track the trends in events over time (10 minutes by default).
        /// </summary>
        public Summary CreateSummary(string name, string help, SummaryConfiguration? configuration = null)
        {
            return _registry.GetOrAdd(new CollectorRegistry.CollectorInitializer<Summary, SummaryConfiguration>(
                (n, h, config) => new Summary(n, h, config.LabelNames, config.SuppressInitialValue, config.Objectives, config.MaxAge, config.AgeBuckets, config.BufferSize),
                name, help, configuration ?? SummaryConfiguration.Default));
        }

        /// <summary>
        /// Histograms track the size and number of events in buckets.
        /// </summary>
        public Histogram CreateHistogram(string name, string help, HistogramConfiguration? configuration = null)
        {
            return _registry.GetOrAdd(new CollectorRegistry.CollectorInitializer<Histogram, HistogramConfiguration>(
                (n, h, config) => new Histogram(n, h, config.LabelNames, config.SuppressInitialValue, config.Buckets),
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
    }
}