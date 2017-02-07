using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Prometheus.Advanced.DataContracts;
using Prometheus.Internal;

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
        readonly static Regex MetricName = new Regex(METRIC_NAME_RE);
        readonly static Regex LabelNameRegex = new Regex("^[a-zA-Z_:][a-zA-Z0-9_:]*$");
        readonly static Regex ReservedLabelRegex = new Regex("^__.*$");
        readonly static LabelValues EmptyLabelValues = new LabelValues(new string[0], new string[0]);
        // ReSharper restore StaticFieldInGenericType

        protected abstract MetricType Type { get; }

        public T Labels(params string[] labelValues)
        {
            var key = new LabelValues(LabelNames, labelValues);
            return GetOrAddLabelled(key);
        }

		public void RemoveLabelled(params string[] labelValues)
		{
			var key = new LabelValues(LabelNames, labelValues);

			T temp;
			_labelledMetrics.TryRemove(key, out temp);
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
                    throw new ArgumentException("Invalid label name!");
                }
                if (ReservedLabelRegex.IsMatch(labelName))
                {
                    throw new ArgumentException("Labels starting with double underscore are reserved!");
                }
            }

            _unlabelledLazy = new Lazy<T>(() => GetOrAddLabelled(EmptyLabelValues));
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