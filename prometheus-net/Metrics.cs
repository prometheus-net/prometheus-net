using System;
using System.Collections.Generic;
using Prometheus.Advanced;
using Prometheus.SummaryImpl;

namespace Prometheus
{
    public static class Metrics
    {
        private static readonly MetricFactory DefaultFactory = new MetricFactory(DefaultCollectorRegistry.Instance);

        public static Counter CreateCounter(string name, string help, params string[] labelNames)
        {
            return DefaultFactory.CreateCounter(name, help, labelNames);
        }

        public static Gauge CreateGauge(string name, string help, params string[] labelNames)
        {
            return DefaultFactory.CreateGauge(name, help, labelNames);
        }

        public static Summary CreateSummary(string name, string help, params string[] labelNames)
        {
            return DefaultFactory.CreateSummary(name, help, labelNames);
        }

        public static Prometheus.Summary CreateSummary(string name, string help, string[] labelNames, IList<QuantileEpsilonPair> objectives, TimeSpan maxAge, int? ageBuckets, int? bufCap)
        {
            return DefaultFactory.CreateSummary(name, help, labelNames, objectives, maxAge, ageBuckets, bufCap);
        }

        public static Histogram CreateHistogram(string name, string help, double[] buckets = null, params string[] labelNames)
        {
            return DefaultFactory.CreateHistogram(name, help, buckets, labelNames);
        }

        public static MetricFactory WithCustomRegistry(ICollectorRegistry registry)
        {
            return new MetricFactory(registry);
        }
    }
}