using System;
using io.prometheus.client;
using Prometheus.Internal;
using MetricFamily = Prometheus.Internal.MetricFamily;

namespace Prometheus
{
    public class Counter : Metric
    {
        private double _value;
        private readonly object _lock = new object();
        internal Counter(MetricFamily family, LabelValues labelValues)
            : base(family, labelValues)
        {
        }

        public void Inc(double increment = 1)
        {
            if (increment < 0)
            {
                throw new InvalidOperationException("Counter cannot go down");
            }

            lock (_lock)
            {
                _value += increment;
            }
        }

        public double Value
        {
            get { return _value; }
        }

        internal override MetricType Type
        {
            get { return MetricType.COUNTER; }
        }

        protected override void Populate(io.prometheus.client.Metric metric)
        {
            metric.counter = new io.prometheus.client.Counter();
            lock (_lock)
            {
                metric.counter.value = _value;
            }
        }

        public Counter Labels(params string[] labelValues)
        {
            return (Counter) Family.GetOrAdd(labelValues, (family, values) => new Counter(family, values));
        }
    }
}