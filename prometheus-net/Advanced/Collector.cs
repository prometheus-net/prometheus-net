using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Prometheus.Internal;

namespace Prometheus.Advanced
{
    public abstract class Child
    {
        private LabelValues _labelValues;

        internal virtual void Init(ICollector parent, LabelValues labelValues)
        {
            _labelValues = labelValues;
        }

        protected abstract void Populate(Metric metric);

        internal Metric Collect()
        {
            var metric = new Metric();
            Populate(metric);
            metric.label = _labelValues.WireLabels;
            //metric.timestamp_ms = (long) (ts.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
            return metric;
        }
    }

    public abstract class Collector<T> : ICollector where T : Child, new()
    {
        private const string METRIC_NAME_RE = "^[a-zA-Z_:][a-zA-Z0-9_:]*$";

        private readonly ConcurrentDictionary<LabelValues, T> _labelledMetrics = new ConcurrentDictionary<LabelValues, T>();
        protected readonly T Unlabelled;

        // ReSharper disable StaticFieldInGenericType
        readonly static Regex MetricName = new Regex(METRIC_NAME_RE);
        readonly static Regex LabelNameRegex = new Regex("^[a-zA-Z_:][a-zA-Z0-9_:]*$");
        readonly static Regex ReservedLabelRegex = new Regex("^__.*$");
        // ReSharper restore StaticFieldInGenericType

        protected abstract MetricType Type { get; }

        public T Labels(params string[] labelValues)
        {
            var key = new LabelValues(_labelNames, labelValues);
            return GetOrAddLabelled(key);
        }

        private T GetOrAddLabelled(LabelValues key)
        {
            return _labelledMetrics.GetOrAdd(key, labels1 =>
            {
                var child = new T();
                child.Init(this, labels1);
                return child;
            });
        }

        protected Collector(string name, string help, string[] labelNames)
        {
            _name = name;
            _help = help;
            _labelNames = labelNames;

            if (!MetricName.IsMatch(name))
            {
                throw new ArgumentException("Metric name must match regex: " + METRIC_NAME_RE);
            }

            foreach (var labelName in labelNames)
            {
                if (!LabelNameRegex.IsMatch(labelName))
                {
                    throw new ArgumentException("Invalid label name!");
                }
                if (ReservedLabelRegex.IsMatch(labelName))
                {
                    throw new ArgumentException("Labels starting with double underscore are reserved!");
                }
            }

            Unlabelled = GetOrAddLabelled(LabelValues.Empty);

        }

        public string Name
        {
            get { return _name; }
        }

        private readonly string _name;
        private readonly string _help;
        private readonly string[] _labelNames;

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