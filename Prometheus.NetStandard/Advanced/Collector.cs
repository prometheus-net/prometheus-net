using Prometheus.Advanced.DataContracts;
using Prometheus.Internal;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;

namespace Prometheus.Advanced
{
    public abstract class Collector<T> : ICollector where T : Child, new()
    {
        private const string METRIC_NAME_RE = "^[a-zA-Z_:][a-zA-Z0-9_:]*$";

        private readonly ConcurrentDictionary<LabelValues, T> _labelledMetrics = new ConcurrentDictionary<LabelValues, T>();
        private readonly string _name;
        private readonly string _help;
        private readonly Lazy<T> _unlabelledLazy;

        // ReSharper disable StaticFieldInGenericType
        readonly static Regex MetricName = new Regex(METRIC_NAME_RE, RegexOptions.Compiled);
        readonly static Regex LabelNameRegex = new Regex("^[a-zA-Z_:][a-zA-Z0-9_:]*$", RegexOptions.Compiled);
        readonly static Regex ReservedLabelRegex = new Regex("^__.*$", RegexOptions.Compiled);
        // ReSharper restore StaticFieldInGenericType

        protected abstract MetricType Type { get; }

        public T Labels(params string[] labelValues)
        {
            if (labelValues.Any(lv => lv == null))
                throw new ArgumentNullException("A label value cannot be null.");

            var key = new LabelValues(LabelNames, labelValues);
            return GetOrAddLabelled(key);
        }

        public void RemoveLabelled(params string[] labelValues)
        {
            if (labelValues.Any(lv => lv == null))
                throw new ArgumentNullException("A label value cannot be null.");

            var key = new LabelValues(LabelNames, labelValues);

            T temp;
            _labelledMetrics.TryRemove(key, out temp);
        }

        private T GetOrAddLabelled(LabelValues key)
        {
            tryagain:
            T val;
            if (_labelledMetrics.TryGetValue(key, out val))
                return val;

            val = new T();
            val.Init(this, key);

            if (_labelledMetrics.TryAdd(key, val))
                return val;

            // If we get here, a child with the same labels was concurrently added by another thread.
            // We do not want to return a different child here, so throw it away and try again.
            goto tryagain;
        }

        protected T Unlabelled
        {
            get { return _unlabelledLazy.Value; }
        }

        protected Collector(string name, string help, string[] labelNames)
        {
            _name = name;
            _help = help;
            LabelNames = labelNames;

            if (!MetricName.IsMatch(name))
            {
                throw new ArgumentException("Metric name must match regex: " + METRIC_NAME_RE);
            }

            foreach (var labelName in labelNames)
            {
                if (!LabelNameRegex.IsMatch(labelName))
                {
                    throw new ArgumentException("Invalid label name: " + labelName);
                }
                if (ReservedLabelRegex.IsMatch(labelName))
                {
                    throw new ArgumentException("Labels starting with double underscore are reserved!");
                }
            }

            _unlabelledLazy = new Lazy<T>(() => GetOrAddLabelled(LabelValues.Empty));
        }

        public string Name
        {
            get { return _name; }
        }

        public string[] LabelNames { get; private set; }

        public MetricFamily Collect()
        {
            var result = new MetricFamily()
            {
                name = _name,
                help = _help,
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