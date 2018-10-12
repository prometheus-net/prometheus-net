using Prometheus.SummaryImpl;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Prometheus.Advanced
{
    public class MetricFactory
    {
        private readonly ICollectorRegistry _registry;

        public MetricFactory(ICollectorRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public Counter CreateCounter(string name, string help, CounterConfiguration configuration)
        {
            configuration = configuration ?? CounterConfiguration.Default;

            var metric = new Counter(name, help, configuration.LabelNames, configuration.SuppressInitialValue);
            return (Counter)_registry.GetOrAdd(metric);
        }

        public Gauge CreateGauge(string name, string help, GaugeConfiguration configuration)
        {
            configuration = configuration ?? GaugeConfiguration.Default;

            var metric = new Gauge(name, help, configuration.LabelNames, configuration.SuppressInitialValue);
            return (Gauge)_registry.GetOrAdd(metric);
        }

        public Summary CreateSummary(string name, string help, SummaryConfiguration configuration)
        {
            configuration = configuration ?? SummaryConfiguration.Default;

            var metric = new Summary(name, help, configuration.LabelNames, configuration.SuppressInitialValue, configuration.Objectives, configuration.MaxAge, configuration.AgeBuckets, configuration.BufferSize);
            return (Summary)_registry.GetOrAdd(metric);
        }

        public Histogram CreateHistogram(string name, string help, HistogramConfiguration configuration)
        {
            configuration = configuration ?? HistogramConfiguration.Default;

            var metric = new Histogram(name, help, configuration.LabelNames, configuration.SuppressInitialValue, configuration.Buckets);
            return (Histogram)_registry.GetOrAdd(metric);
        }

        public Counter CreateCounter(string name, string help, params string[] labelNames) =>
            CreateCounter(name, help, new CounterConfiguration
            {
                LabelNames = labelNames
            });

        public Gauge CreateGauge(string name, string help, params string[] labelNames) =>
            CreateGauge(name, help, new GaugeConfiguration
            {
                LabelNames = labelNames
            });

        public Summary CreateSummary(string name, string help, params string[] labelNames) =>
            CreateSummary(name, help, new SummaryConfiguration
            {
                LabelNames = labelNames
            });

        public Summary CreateSummary(string name, string help, string[] labelNames, IList<QuantileEpsilonPair> objectives, TimeSpan? maxAge, int? ageBuckets, int? bufCap)
        {
            var config = new SummaryConfiguration
            {
                LabelNames = labelNames
            };

            if (objectives != null)
                config.Objectives = objectives.ToArray();

            if (maxAge != null)
                config.MaxAge = maxAge.Value;

            if (ageBuckets != null)
                config.AgeBuckets = ageBuckets.Value;

            if (bufCap != null)
                config.BufferSize = bufCap.Value;

            return CreateSummary(name, help, config);
        }

        public Histogram CreateHistogram(string name, string help, double[] buckets = null, params string[] labelNames) =>
            CreateHistogram(name, help, new HistogramConfiguration
            {
                LabelNames = labelNames,
                Buckets = buckets
            });
    }
}