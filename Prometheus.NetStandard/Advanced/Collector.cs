using Prometheus.Advanced.DataContracts;
using Prometheus.Internal;
using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Prometheus.Advanced
{
    public abstract class Collector<TChild> : ICollector where TChild : Child, new()
    {
        private const string ValidMetricNameExpression = "^[a-zA-Z_:][a-zA-Z0-9_:]*$";
        private const string ValidLabelNameExpression = "^[a-zA-Z_:][a-zA-Z0-9_:]*$";
        private const string ReservedLabelNameExpression = "^__.*$";

        private readonly ConcurrentDictionary<LabelValues, TChild> _labelledMetrics = new ConcurrentDictionary<LabelValues, TChild>();
        private readonly Lazy<TChild> _unlabelledLazy;

        // ReSharper disable StaticFieldInGenericType
        private readonly static Regex MetricNameRegex = new Regex(ValidMetricNameExpression, RegexOptions.Compiled);
        private readonly static Regex LabelNameRegex = new Regex(ValidLabelNameExpression, RegexOptions.Compiled);
        private readonly static Regex ReservedLabelRegex = new Regex(ReservedLabelNameExpression, RegexOptions.Compiled);
        // ReSharper restore StaticFieldInGenericType

        public TChild Labels(params string[] labelValues)
        {
            var key = new LabelValues(LabelNames, labelValues);
            return GetOrAddLabelled(key);
        }

        public void RemoveLabelled(params string[] labelValues)
        {
            var key = new LabelValues(LabelNames, labelValues);
            _labelledMetrics.TryRemove(key, out _);
        }

        private TChild GetOrAddLabelled(LabelValues key)
        {
            tryagain:
            if (_labelledMetrics.TryGetValue(key, out var existing))
                return existing;

            var child = new TChild();
            child.Init(this, key);

            if (_labelledMetrics.TryAdd(key, child))
                return child;

            // If we get here, a child with the same labels was concurrently added by another thread.
            // We do not want to return a different child here, so throw it away and try again.
            goto tryagain;
        }

        protected Collector(string name, string help, string[] labelNames)
        {
            if (!MetricNameRegex.IsMatch(name))
            {
                throw new ArgumentException($"Metric name '{name}' does not match regex '{ValidMetricNameExpression}'.");
            }

            foreach (var labelName in labelNames)
            {
                if (labelName == null)
                {
                    throw new ArgumentNullException("Label name was null.");
                }

                if (!LabelNameRegex.IsMatch(labelName))
                {
                    throw new ArgumentException($"Label name '{labelName}' does not match regex '{ValidLabelNameExpression}'.");
                }

                if (ReservedLabelRegex.IsMatch(labelName))
                {
                    throw new ArgumentException($"Label name '{labelName}' is not valid - labels starting with double underscore are reserved!");
                }
            }

            Name = name;
            Help = help;
            LabelNames = labelNames;

            _unlabelledLazy = new Lazy<TChild>(() => GetOrAddLabelled(LabelValues.Empty));
        }

        public string Name { get; }
        public string Help { get; }

        public string[] LabelNames { get; }

        protected abstract MetricType Type { get; }

        protected TChild Unlabelled
        {
            get { return _unlabelledLazy.Value; }
        }

        public MetricFamily Collect()
        {
            var result = new MetricFamily()
            {
                name = Name,
                help = Help,
                type = Type,
            };

            foreach (var child in _labelledMetrics.Values)
            {
                result.metric.Add(child.Collect());
            }

            return result;
        }
    }
}