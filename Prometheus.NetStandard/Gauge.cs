namespace Prometheus
{
    public interface IGauge
    {
        void Inc(double increment = 1);
        void Set(double val);
        void Dec(double decrement = 1);
        double Value { get; }
    }

    public sealed class Gauge : Collector<Gauge.Child>, IGauge
    {
        public sealed class Child : ChildBase, IGauge
        {
            private ThreadSafeDouble _value;

            internal override void Populate(MetricData metric)
            {
                metric.Gauge = new GaugeData
                {
                    Value = Value
                };
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

            public void Dec(double decrement = 1)
            {
                Inc(-decrement);
            }

            public double Value => _value.Value;
        }

        internal Gauge(string name, string help, string[] labelNames, bool suppressInitialValue)
    : base(name, help, labelNames, suppressInitialValue)
        {
        }

        public void Inc(double increment = 1) => Unlabelled.Inc(increment);
        public void Set(double val) => Unlabelled.Set(val);
        public void Dec(double decrement = 1) => Unlabelled.Dec(decrement);
        public double Value => Unlabelled.Value;

        public void Publish() => Unlabelled.Publish();

        internal override MetricType Type => MetricType.Gauge;
    }
}