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
        public Counter CreateCounter(string name, string help, CounterConfiguration configuration = null) =>
            _registry.CreateCounter(name, help, configuration);

        /// <summary>
        /// Gauges can have any numeric value and change arbitrarily.
        /// </summary>
        public Gauge CreateGauge(string name, string help, GaugeConfiguration configuration = null) =>
            _registry.CreateGauge(name, help, configuration);

        /// <summary>
        /// Summaries track the trends in events over time (10 minutes by default).
        /// </summary>
        public Summary CreateSummary(string name, string help, SummaryConfiguration configuration = null) =>
            _registry.CreateSummary(name, help, configuration);

        /// <summary>
        /// Histograms track the size and number of events in buckets.
        /// </summary>
        public Histogram CreateHistogram(string name, string help, HistogramConfiguration configuration = null) =>
            _registry.CreateHistogram(name, help, configuration);

        /// <summary>
        /// Counters only increase in value and reset to zero when the process restarts.
        /// </summary>
        public Counter CreateCounter(string name, string help, params string[] labelNames) =>
            _registry.CreateCounter(name, help, null, labelNames);

        /// <summary>
        /// Gauges can have any numeric value and change arbitrarily.
        /// </summary>
        public Gauge CreateGauge(string name, string help, params string[] labelNames) =>
            _registry.CreateGauge(name, help, null, labelNames);

        /// <summary>
        /// Summaries track the trends in events over time (10 minutes by default).
        /// </summary>
        public Summary CreateSummary(string name, string help, params string[] labelNames) =>
            _registry.CreateSummary(name, help, null, labelNames);

        /// <summary>
        /// Histograms track the size and number of events in buckets.
        /// </summary>
        public Histogram CreateHistogram(string name, string help, params string[] labelNames) =>
            _registry.CreateHistogram(name, help, null, labelNames);
    }
}