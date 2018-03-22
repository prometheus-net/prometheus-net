using Prometheus.SummaryImpl;
using System;
using System.Collections.Generic;

namespace Prometheus.Advanced
{
    public class MetricFactory
    {
        private readonly ICollectorRegistry _registry;

        public MetricFactory(ICollectorRegistry registry)
        {
            _registry = registry;
        }

        public Counter CreateCounter(string name, string help, params string[] labelNames)
        {
            var metric = new Counter(name, help, labelNames);
            return (Counter)_registry.GetOrAdd(metric);
        }

        public Gauge CreateGauge(string name, string help, params string[] labelNames)
        {
            var metric = new Gauge(name, help, labelNames);
            return (Gauge)_registry.GetOrAdd(metric);
        }

        public Summary CreateSummary(string name, string help, params string[] labelNames)
        {
            var metric = new Summary(name, help, labelNames);
            return (Summary)_registry.GetOrAdd(metric);
        }

        public Summary CreateSummary(
            string name,
            string help,
            string[] labelNames,
            IList<QuantileEpsilonPair> objectives = null,
            TimeSpan? maxAge = null,
            int? ageBuckets = null,
            int? bufCap = null)
        {
            var metric = new Summary(name, help, labelNames, objectives, maxAge, ageBuckets, bufCap);
            return (Summary)_registry.GetOrAdd(metric);
        }

        public Histogram CreateHistogram(string name, string help, double[] buckets = null, params string[] labelNames)
        {
            var metric = new Histogram(name, help, labelNames, buckets);
            return (Histogram)_registry.GetOrAdd(metric);
        }
    }
}