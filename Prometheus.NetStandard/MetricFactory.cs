using System;

namespace Prometheus
{
    /// <summary>
    /// Adds metrics to a registry.
    /// </summary>
    public sealed class MetricFactory
    {
        private readonly CollectorRegistry _registry;

        public MetricFactory(CollectorRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// Counters only increase in value and reset to zero when the process restarts.
        /// </summary>
        public Counter CreateCounter(string name, string help, CounterConfiguration configuration = null)
        {
            configuration = configuration ?? CounterConfiguration.Default;

            var metric = new Counter(name, help, configuration.LabelNames, configuration.SuppressInitialValue);
            return (Counter)_registry.GetOrAdd(metric);
        }

        /// <summary>
        /// Gauges can have any numeric value and change arbitrarily.
        /// </summary>
        public Gauge CreateGauge(string name, string help, GaugeConfiguration configuration = null)
        {
            configuration = configuration ?? GaugeConfiguration.Default;

            var metric = new Gauge(name, help, configuration.LabelNames, configuration.SuppressInitialValue);
            return (Gauge)_registry.GetOrAdd(metric);
        }

        /// <summary>
        /// Summaries track the trends in events over time (10 minutes by default).
        /// </summary>
        public Summary CreateSummary(string name, string help, SummaryConfiguration configuration = null)
        {
            configuration = configuration ?? SummaryConfiguration.Default;

            var metric = new Summary(name, help, configuration.LabelNames, configuration.SuppressInitialValue, configuration.Objectives, configuration.MaxAge, configuration.AgeBuckets, configuration.BufferSize);
            return (Summary)_registry.GetOrAdd(metric);
        }

        /// <summary>
        /// Histograms track the size and number of events in buckets.
        /// </summary>
        public Histogram CreateHistogram(string name, string help, HistogramConfiguration configuration = null)
        {
            configuration = configuration ?? HistogramConfiguration.Default;

            var metric = new Histogram(name, help, configuration.LabelNames, configuration.SuppressInitialValue, configuration.Buckets);
            return (Histogram)_registry.GetOrAdd(metric);
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