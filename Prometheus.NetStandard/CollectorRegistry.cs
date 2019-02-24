using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Prometheus
{
    /// <summary>
    /// Maintains references to a set of collectors, from which data for metrics is collected at data export time.
    /// 
    /// Use methods on the <see cref="Metrics"/> class to add metrics to a collector registry.
    /// </summary>
    /// <remarks>
    /// To encourage good concurrency practices, registries are append-only. You can add things to them but not remove.
    /// If you wish to remove things from the registry, create a new registry with only the things you wish to keep.
    /// </remarks>
    public sealed class CollectorRegistry
    {
        private readonly ConcurrentBag<Action> _beforeCollectCallbacks = new ConcurrentBag<Action>();
        private readonly ConcurrentDictionary<string, Collector> _collectors = new ConcurrentDictionary<string, Collector>();

        /// <summary>
        /// Counters only increase in value and reset to zero when the process restarts.
        /// </summary>
        public Counter CreateCounter(string name, string help, CounterConfiguration configuration = null, params string[] labelNames)
        {
            configuration = configuration ?? CounterConfiguration.Default;
            labelNames = labelNames.Length == 0 ? configuration.LabelNames : labelNames;
            
            var collector = _collectors.GetOrAdd(
                name,
                key => new Counter(
                    name, 
                    help,
                    labelNames,
                    configuration.SuppressInitialValue));

            VerifyLabels(collector, labelNames);

            return (Counter)collector;
        }

        /// <summary>
        /// Gauges can have any numeric value and change arbitrarily.
        /// </summary>
        public Gauge CreateGauge(string name, string help, GaugeConfiguration configuration = null, params string[] labelNames)
        {
            configuration = configuration ?? GaugeConfiguration.Default;
            labelNames = labelNames.Length == 0 ? configuration.LabelNames : labelNames;

            var collector = _collectors.GetOrAdd(
                name,
                key => new Gauge(
                    name, 
                    help,
                    labelNames,
                    configuration.SuppressInitialValue));

            VerifyLabels(collector, labelNames);

            return (Gauge)collector;
        }

        /// <summary>
        /// Summaries track the trends in events over time (10 minutes by default).
        /// </summary>
        public Summary CreateSummary(string name, string help, SummaryConfiguration configuration = null, params string[] labelNames)
        {
            configuration = configuration ?? SummaryConfiguration.Default;
            labelNames = labelNames.Length == 0 ? configuration.LabelNames : labelNames;

            var collector = _collectors.GetOrAdd(
                name,
                key => new Summary(
                    name, 
                    help,
                    labelNames,
                    configuration.SuppressInitialValue,
                    configuration.Objectives, 
                    configuration.MaxAge,
                    configuration.AgeBuckets,
                    configuration.BufferSize));

            VerifyLabels(collector, labelNames);

            return (Summary)collector;
        }

        /// <summary>
        /// Histograms track the size and number of events in buckets.
        /// </summary>
        public Histogram CreateHistogram(string name, string help, HistogramConfiguration configuration = null, params string[] labelNames)
        {
            configuration = configuration ?? HistogramConfiguration.Default;
            labelNames = labelNames.Length == 0 ? configuration.LabelNames : labelNames;
            
            var collector = _collectors.GetOrAdd(
                name,
                key => new Histogram(
                    name, 
                    help,
                    labelNames,
                    configuration.SuppressInitialValue, 
                    configuration.Buckets));

            VerifyLabels(collector, labelNames);

            return (Histogram)collector;
        }

        /// <summary>
        /// Registers an action to be called before metrics are collected.
        /// This enables you to do last-minute updates to metric values very near the time of collection.
        /// 
        /// If the callback throws <see cref="ScrapeFailedException"/> then the entire metric collection will fail.
        /// This will result in an appropriate HTTP error code or a skipped push, depending on type of exporter.
        /// </summary>
        public void AddBeforeCollectCallback(Action callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            _beforeCollectCallbacks.Add(callback);
        }
        
        /// <summary>
        /// Collects metrics from all the registered collectors and sends them to the specified serializer.
        /// </summary>
        internal void CollectAndSerialize(IMetricsSerializer serializer)
        {
            foreach (var callback in _beforeCollectCallbacks)
                callback();

            foreach (var collector in _collectors.Values)
                collector.CollectAndSerialize(serializer);
        }

        private static void VerifyLabels(Collector collector, string[] labels)
        {
            if (collector.LabelNames == null || labels == null)
            {
                return;
            }

            if (collector.LabelNames.SequenceEqual(labels) == false)
            {
                throw new InvalidOperationException("Collector with same name must have same label names");
            }
        }
    }
}
