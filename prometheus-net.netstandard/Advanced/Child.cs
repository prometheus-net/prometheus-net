using Prometheus.Advanced.DataContracts;
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
}