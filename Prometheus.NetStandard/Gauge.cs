using Prometheus.Advanced;
using Prometheus.Advanced.DataContracts;

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
        internal Gauge(string name, string help, string[] labelNames)
            : base(name, help, labelNames)
        {
        }

        public class Timer
        {
            private System.Diagnostics.Stopwatch _stopwatch;
            private IGauge _gauge;

            public Timer(IGauge gauge)
            {
                _gauge = gauge;
                _stopwatch = System.Diagnostics.Stopwatch.StartNew();
            }

            public void ApplyDuration()
            {
                _gauge.Set(_stopwatch.Elapsed.TotalSeconds);
            }
        }

        public class Child : Advanced.Child, IGauge
        {
            private ThreadSafeDouble _value;

            protected override void Populate(Metric metric)
            {
                metric.gauge = new Advanced.DataContracts.Gauge();
                metric.gauge.value = Value;
            }

            public void Inc(double increment = 1)
            {
                _value.Add(increment);
            }

            public void Set(double val)
            {
                _value.Value = val;
            }
            
            public Gauge.Timer StartTimer()
            {
                return new Gauge.Timer(this);
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
    }
}