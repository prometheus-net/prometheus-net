using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Prometheus.Internal
{
    internal class MetricFamily
    {
        private const string METRIC_NAME_RE = "^[a-zA-Z_:][a-zA-Z0-9_:]*$";

        private readonly ConcurrentDictionary<LabelValues, Metric> _labelledMetrics = new ConcurrentDictionary<LabelValues, Metric>();
        private readonly io.prometheus.client.MetricFamily _wireMetricFamily;
        readonly static Regex MetricName = new Regex(METRIC_NAME_RE);
        readonly static Regex LabelNameRegex = new Regex("^[a-zA-Z_:][a-zA-Z0-9_:]*$");
        readonly static Regex ReservedLabelRegex = new Regex("^__.*$");
        private readonly Type _metricType;

        public MetricFamily(string name, string help, Type metricType, string[] labelNames)
        {
            _labelNames = labelNames;
            _metricType = metricType;
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


            _wireMetricFamily = new io.prometheus.client.MetricFamily()
            {
                help = help,
                name = name,
            };
        }

        public string Name
        {
            get { return _wireMetricFamily.name; }
        }

        public Type MetricType
        {
            get { return _metricType; }
        }

        private readonly string[] _labelNames;
        

        //this is not thread-safe, but that's fine as it's only called from one thread at a time
        public io.prometheus.client.MetricFamily Collect()
        {
            _wireMetricFamily.metric.Clear();

            foreach (var metric in _labelledMetrics.Values)
            {
                _wireMetricFamily.metric.Add(metric.Collect());
            }

            return _wireMetricFamily;
        }

        public void Register(LabelValues labelValues, Metric metric)
        {
            _labelledMetrics[labelValues] = metric;
            _wireMetricFamily.type = metric.Type;
        }

        public Metric GetOrAdd(string[] labelValues, Func<MetricFamily, LabelValues, Metric> func)
        {
            var key = new LabelValues(_labelNames, labelValues);
            return _labelledMetrics.GetOrAdd(key, labels1 => func(this, labels1));
        }
    }
}