using Prometheus.Advanced.DataContracts;
using Prometheus.Internal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
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

        // This servers a slightly silly but useful purpose: by default if you start typing .La... and trigger Intellisense
        // it will often for whatever reason focus on LabelNames instead of Labels, leading to tiny but persistent frustration.
        // Having WithLabels() instead eliminates the other candidate and allows for a frustration-free typing experience.
        public TChild WithLabels(params string[] labelValues) => Labels(labelValues);

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
            child.Init(this, key, publish: !_suppressInitialValue);

            if (_labelledMetrics.TryAdd(key, child))
                return child;

            // If we get here, a child with the same labels was concurrently added by another thread.
            // We do not want to return a different child here, so throw it away and try again.
            goto tryagain;
        }

        private static readonly string[] EmptyLabelNames = new string[0];

        protected Collector(string name, string help, string[] labelNames, bool suppressInitialValue)
        {
            labelNames = labelNames ?? EmptyLabelNames;

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

            _suppressInitialValue = suppressInitialValue;

            _unlabelledLazy = new Lazy<TChild>(() => GetOrAddLabelled(LabelValues.Empty));
        }

        public string Name { get; }
        public string Help { get; }

        public string[] LabelNames { get; }

        protected abstract MetricType Type { get; }

        private readonly bool _suppressInitialValue;

        protected TChild Unlabelled
        {
            get { return _unlabelledLazy.Value; }
        }

        public IEnumerable<MetricFamily> Collect(NameValueCollection queryParameters = null)
        {
            EnsureUnlabelledMetricCreatedIfNoLabels();

            var result = new MetricFamily()
            {
                name = Name,
                help = Help,
                type = Type,
            };

            foreach (var child in _labelledMetrics.Values)
            {
                var metric = child.Collect();

                if (metric == null)
                    continue; // This can occur due to initial value suppression.

                result.metric.Add(metric);
            }

            yield return result;
        }

        private void EnsureUnlabelledMetricCreatedIfNoLabels()
        {
            // We want metrics to exist even with 0 values if they are supposed to be used without labels.
            // Labelled metrics are created when label values are assigned. However, as unlabelled metrics are lazy-created
            // (they might are optional if labels are used) we might lose them for cases where they really are desired.

            // If there are no label names then clearly this metric is supposed to be used unlabelled, so create it.
            // Otherwise, we allow unlabelled metrics to be used if the user explicitly does it but omit them by default.
            if (!_unlabelledLazy.IsValueCreated && !LabelNames.Any())
                GetOrAddLabelled(LabelValues.Empty);
        }
    }
}