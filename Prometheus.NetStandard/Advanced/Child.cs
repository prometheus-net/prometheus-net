using System.Collections.Specialized;
using Prometheus.Advanced.DataContracts;
using Prometheus.Internal;

namespace Prometheus.Advanced
{
    public abstract class Child
    {
        /// <summary>
        /// Marks the metric as one to be published, even if it might otherwise be suppressed.
        /// This is useful for publishing zero-valued metrics once you have loaded data and determined
        /// that there is no data to actually include in the metric.
        /// </summary>
        public void Publish()
        {
            _publish = true;
        }

        private LabelValues _labelValues;

        // Subclasses must set this to true when the value of the metric is modified, to signal
        // that the metric should now be published if it was explicitly suppressed beforehand.
        protected volatile bool _publish;

        internal virtual void Init(ICollector parent, LabelValues labelValues, bool publish)
        {
            _labelValues = labelValues;
            _publish = publish;
        }

        protected abstract void Populate(Metric metric);

        internal Metric Collect()
        {
            if (!_publish)
                return null;

            var metric = new Metric
            {
                label = _labelValues.WireLabels
            };

            Populate(metric);

            return metric;
        }
    }
}