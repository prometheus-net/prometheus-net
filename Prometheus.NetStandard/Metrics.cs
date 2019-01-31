using Prometheus.SummaryImpl;
using System;
using System.Collections.Generic;

namespace Prometheus
{
    /// <summary>
    /// Static class for easy creation of metrics.
    /// 
    /// Created metrics are registered in <see cref="DefaultCollectorRegistry.Instance"/>.
    /// </summary>
    public static class Metrics
    {
        private static readonly MetricFactory DefaultFactory = new MetricFactory(DefaultCollectorRegistry.Instance);

        /// <summary>
        /// Returns an instance you can use to register metrics in a custom registry.
        /// </summary>
        public static MetricFactory WithCustomRegistry(ICollectorRegistry registry) =>
            new MetricFactory(registry);

        /// <summary>
        /// Counters only increase in value and reset to zero when the process restarts.
        /// </summary>
        public static Counter CreateCounter(string name, string help, CounterConfiguration configuration) =>
            DefaultFactory.CreateCounter(name, help, configuration);

        /// <summary>
        /// Gauges can have any numeric value and change arbitrarily.
        /// </summary>
        public static Gauge CreateGauge(string name, string help, GaugeConfiguration configuration) =>
            DefaultFactory.CreateGauge(name, help, configuration);

        /// <summary>
        /// Summaries track the trends in events over time (10 minutes by default).
        /// </summary>
        public static Summary CreateSummary(string name, string help, SummaryConfiguration configuration) =>
            DefaultFactory.CreateSummary(name, help, configuration);

        /// <summary>
        /// Histograms track the size and number of events in buckets.
        /// </summary>
        public static Histogram CreateHistogram(string name, string help, HistogramConfiguration configuration) =>
            DefaultFactory.CreateHistogram(name, help, configuration);

        /// <summary>
        /// Counters only increase in value and reset to zero when the process restarts.
        /// </summary>
        public static Counter CreateCounter(string name, string help, params string[] labelNames) =>
            DefaultFactory.CreateCounter(name, help, labelNames);

        /// <summary>
        /// Gauges can have any numeric value and change arbitrarily.
        /// </summary>
        public static Gauge CreateGauge(string name, string help, params string[] labelNames) =>
            DefaultFactory.CreateGauge(name, help, labelNames);

        /// <summary>
        /// Summaries track the trends in events over time (10 minutes by default).
        /// </summary>
        public static Summary CreateSummary(string name, string help, params string[] labelNames) =>
            DefaultFactory.CreateSummary(name, help, labelNames);

        /// <summary>
        /// Summaries track the trends in events over time (10 minutes by default).
        /// </summary>
        public static Summary CreateSummary(string name, string help, string[] labelNames, IList<QuantileEpsilonPair> objectives, TimeSpan? maxAge, int? ageBuckets, int? bufCap) =>
            DefaultFactory.CreateSummary(name, help, labelNames, objectives, maxAge, ageBuckets, bufCap);

        /// <summary>
        /// Histograms track the size and number of events in buckets.
        /// </summary>
        public static Histogram CreateHistogram(string name, string help, double[] buckets = null, params string[] labelNames) =>
            DefaultFactory.CreateHistogram(name, help, buckets, labelNames);
    }
}