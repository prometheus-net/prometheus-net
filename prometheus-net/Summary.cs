using Prometheus.Advanced;
using Prometheus.Advanced.DataContracts;

namespace Prometheus
{
    public class Summary : Collector<Summary.Child>
    {
        public class Child : Advanced.Child
        {
            private double _sum = 0;
            private ulong _count = 0;
            private readonly object _lock = new object();

            protected override void Populate(Metric metric)
            {
                metric.summary = new Advanced.DataContracts.Summary();
                lock (_lock)
                {
                    metric.summary.sample_count = _count;
                    metric.summary.sample_sum = _sum;
                }
            }

            public void Observe(double val)
            {
                lock (_lock)
                {
                    _sum += val;
                    _count += 1;
                }
            }
        }

        public void Observe(double val)
        {
            Unlabelled.Observe(val);
        }

        internal Summary(string name, string help, string[] labelNames) : base(name, help, labelNames)
        {
        }

        protected override MetricType Type
        {
            get { return MetricType.SUMMARY; }
        }

    }
}