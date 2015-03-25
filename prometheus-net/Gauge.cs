using System;
using System.Threading;
using Prometheus.Internal;
using MetricType = io.prometheus.client.MetricType;

namespace Prometheus
{
    public class Gauge : Metric
    {
        private double _value;
        private readonly object _lock = new object();

        internal Gauge(MetricFamily family, LabelValues labelValues)
            : base(family, labelValues)
        {
        }

        public void Inc(double increment = 1)
        {
            lock (_lock)
            {
                _value += increment;
            }
        }

        public void Observe(double val)
        {
            Interlocked.Exchange(ref _value, val);
        }


        public void Dec(double decrement = 1)
        {
            Inc(-decrement);
        }

        public Gauge Labels(params string[] labelValues)
        {
            return (Gauge) Family.GetOrAdd(labelValues, (family, values) => new Gauge(family, values));
        }

        internal override MetricType Type
        {
            get { return MetricType.GAUGE; }
        }

        public double Value
        {
            get { return _value; }
        }

        protected override void Populate(io.prometheus.client.Metric metric)
        {
            metric.gauge = new io.prometheus.client.Gauge();
            lock (_lock)
            {
                metric.gauge.value = _value;
            }
        }
    }
}