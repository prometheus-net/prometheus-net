using Prometheus.Advanced;
using Prometheus.SummaryImpl;
using System;
using System.Collections.Generic;

namespace Prometheus
{
    public static class Metrics
    {
        private static readonly MetricFactory DefaultFactory = new MetricFactory(DefaultCollectorRegistry.Instance);

        public static MetricFactory WithCustomRegistry(ICollectorRegistry registry) =>
            new MetricFactory(registry);

        public static Counter CreateCounter(string name, string help, CounterConfiguration configuration) =>
            DefaultFactory.CreateCounter(name, help, configuration);

        public static Gauge CreateGauge(string name, string help, GaugeConfiguration configuration) =>
            DefaultFactory.CreateGauge(name, help, configuration);

        public static Summary CreateSummary(string name, string help, SummaryConfiguration configuration) =>
            DefaultFactory.CreateSummary(name, help, configuration);

        public static Histogram CreateHistogram(string name, string help, HistogramConfiguration configuration) =>
            DefaultFactory.CreateHistogram(name, help, configuration);

        public static Counter CreateCounter(string name, string help, params string[] labelNames) =>
            DefaultFactory.CreateCounter(name, help, labelNames);

        public static Gauge CreateGauge(string name, string help, params string[] labelNames) =>
            DefaultFactory.CreateGauge(name, help, labelNames);

        public static Summary CreateSummary(string name, string help, params string[] labelNames) =>
            DefaultFactory.CreateSummary(name, help, labelNames);

        public static Summary CreateSummary(string name, string help, string[] labelNames, IList<QuantileEpsilonPair> objectives, TimeSpan? maxAge, int? ageBuckets, int? bufCap) =>
            DefaultFactory.CreateSummary(name, help, labelNames, objectives, maxAge, ageBuckets, bufCap);

        public static Histogram CreateHistogram(string name, string help, double[] buckets = null, params string[] labelNames) =>
            DefaultFactory.CreateHistogram(name, help, buckets, labelNames);
    }
}