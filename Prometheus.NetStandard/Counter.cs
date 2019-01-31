using System;

namespace Prometheus
{
    public interface ICounter
    {
        void Inc(double increment = 1);
        double Value { get; }
    }

    public sealed class Counter : Collector<Counter.Child>, ICounter
    {
        public sealed class Child : ChildBase, ICounter
        {
            private ThreadSafeDouble _value;

            internal override void Populate(MetricData metric)
            {
                metric.Counter = new CounterData
                {
                    Value = Value
                };
            }

            public void Inc(double increment = 1.0)
            {
                // Note: Prometheus recommendations are that this assert > 0. However, there are times your
                // measurement results in a zero and it's easier to have the counter handle this elegantly.
                if (increment < 0.0)
                    throw new ArgumentOutOfRangeException("increment", "Counter cannot go down");

                _value.Add(increment);
                _publish = true;
            }

            public double Value => _value.Value;
        }

        internal Counter(string name, string help, string[] labelNames, bool suppressInitialValue)
            : base(name, help, labelNames, suppressInitialValue)
        {
        }

        public void Inc(double increment = 1) => Unlabelled.Inc(increment);
        public double Value => Unlabelled.Value;

        public void Publish() => Unlabelled.Publish();

        internal override MetricType Type => MetricType.Counter;
    }
}