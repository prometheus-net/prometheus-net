using System;
using Prometheus.DataContracts;

namespace Prometheus
{
    public interface IGauge
    {
        void Inc(double increment = 1);
        void Set(double val);
        void Dec(double decrement = 1);
        double Value { get; }
    }

    public class Gauge : Collector<Gauge.Child>, IGauge
    {
        internal Gauge(string name, string help, string[] labelNames, bool suppressInitialValue)
            : base(name, help, labelNames, suppressInitialValue)
        {
        }

        public class Child : Prometheus.Child, IGauge
        {
            private ThreadSafeDouble _value;

            protected override void Populate(Metric metric)
            {
                metric.gauge = new DataContracts.Gauge();
                metric.gauge.value = Value;
            }

            public void Inc(double increment = 1)
            {
                _value.Add(increment);
                _publish = true;
            }

            public void Set(double val)
            {
                _value.Value = val;
                _publish = true;
            }
            
            public Timer StartTimer()
            {
                return new Timer(this);
            }

            public void Dec(double decrement = 1)
            {
                Inc(-decrement);
            }

            public double Value => _value.Value;
        }

        protected override MetricType Type => MetricType.GAUGE;

        public void Inc(double increment = 1) => Unlabelled.Inc(increment);
        public void Set(double val) => Unlabelled.Set(val);
        public void Dec(double decrement = 1) => Unlabelled.Dec(decrement);
        public double Value => Unlabelled.Value;

        public void Publish() => Unlabelled.Publish();
    }
}