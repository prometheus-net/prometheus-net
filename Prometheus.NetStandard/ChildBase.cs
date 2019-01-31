namespace Prometheus
{
    /// <summary>
    /// Base class for labeled instances of metrics (with all label names and label values defined).
    /// </summary>
    public abstract class ChildBase
    {
        /// <summary>
        /// Marks the metric as one to be published, even if it might otherwise be suppressed.
        /// 
        /// This is useful for publishing zero-valued metrics once you have loaded data on startup and determined
        /// that there is no need to increment the value of the metric.
        /// </summary>
        public void Publish()
        {
            _publish = true;
        }

        private LabelValues _labelValues;

        // Subclasses must set this to true when the value of the metric is modified, to signal
        // that the metric should now be published if it was explicitly suppressed beforehand.
        protected volatile bool _publish;

        internal virtual void Init(Collector parent, LabelValues labelValues, bool publish)
        {
            _labelValues = labelValues;
            _publish = publish;
        }

        internal abstract void Populate(MetricData metric);

        internal MetricData Collect()
        {
            if (!_publish)
                return null;

            var metric = new MetricData
            {
                Labels = _labelValues.WireLabels
            };

            Populate(metric);

            return metric;
        }
    }
}