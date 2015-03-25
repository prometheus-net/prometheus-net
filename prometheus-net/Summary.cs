using io.prometheus.client;
using Prometheus.Internal;
using MetricFamily = Prometheus.Internal.MetricFamily;

namespace Prometheus
{
    public class Summary : Metric
    {
        private double _sum = 0;
        private ulong _count = 0;
        private readonly object _lock = new object();

        internal Summary(MetricFamily family, LabelValues labelValues)
            : base(family, labelValues)
        {
        }

        public void Observe(double val)
        {
            lock (_lock)
            {
                _sum += val;
                _count += 1;
            }
        }

        public Summary Labels(params string[] labelValues)
        {
            return (Summary) Family.GetOrAdd(labelValues, (family, values) => new Summary(family, values));
        }

        internal override MetricType Type
        {
            get { return MetricType.SUMMARY; }
        }

        protected override void Populate(io.prometheus.client.Metric metric)
        {
            metric.summary = new io.prometheus.client.Summary();
            lock (_lock)
            {
                metric.summary.sample_count = _count;
                metric.summary.sample_sum = _sum;
            }
        }
    }
}