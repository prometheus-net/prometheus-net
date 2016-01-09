using System;
using System.Collections.Generic;
using Prometheus.SummaryImpl;

namespace Prometheus.Advanced
{
    public class MetricFactory
    {
        private readonly ICollectorRegistry _registry;

        public MetricFactory(ICollectorRegistry registry)
        {
            _registry = registry;
        }

        public Prometheus.Counter CreateCounter(string name, string help, params string[] labelNames)
        {
            var metric = new Prometheus.Counter(name, help, labelNames);
            return (Prometheus.Counter) _registry.GetOrAdd(metric);
        }

        public Prometheus.Gauge CreateGauge(string name, string help, params string[] labelNames)
        {
            var metric = new Prometheus.Gauge(name, help, labelNames);
            return (Prometheus.Gauge) _registry.GetOrAdd(metric);
        }

        public Prometheus.Summary CreateSummary(string name, string help, params string[] labelNames)
        {
            var metric = new Prometheus.Summary(name, help, labelNames);
            return (Prometheus.Summary) _registry.GetOrAdd(metric);
        }

        public Prometheus.Summary CreateSummary(string name, string help, string[] labelNames, IList<QuantileEpsilonPair> objectives, TimeSpan maxAge, int? ageBuckets, int? bufCap)
        {
            var metric = new Prometheus.Summary(name, help, labelNames, objectives, maxAge, ageBuckets, bufCap);
            return (Prometheus.Summary)_registry.GetOrAdd(metric);
        }

        public Prometheus.Histogram CreateHistogram(string name, string help, double[] buckets = null, params string[] labelNames)
        {
            var metric = new Prometheus.Histogram(name, help, labelNames, buckets);
            return (Prometheus.Histogram) _registry.GetOrAdd(metric);
        }
    }
}