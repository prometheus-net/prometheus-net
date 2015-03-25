using io.prometheus.client;
using Prometheus.Internal;
using MetricFamily = Prometheus.Internal.MetricFamily;

namespace Prometheus
{
    public abstract class Metric
    {
        internal readonly MetricFamily Family;
        private readonly LabelValues _labelValues;

        internal Metric(MetricFamily family, LabelValues labelValues)
        {
            _labelValues = labelValues;
            Family = family;
            Family.Register(labelValues, this);
        }

        //public override string ToString()
        //{
        //    return string.Format("{0}({1}) - {2}", _family.Name, typeof(T).Name, _labelValues.ToString());
        //}

        internal abstract MetricType Type { get; }

        protected abstract void Populate(io.prometheus.client.Metric metric);

        internal io.prometheus.client.Metric Collect()
        {
            var metric = new io.prometheus.client.Metric();
            Populate(metric);
            metric.label = _labelValues.WireLabels;
            //metric.timestamp_ms = (long) (ts.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
            return metric;
        }
    }
}