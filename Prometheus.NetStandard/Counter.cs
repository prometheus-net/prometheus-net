using Prometheus.DataContracts;
using System;

namespace Prometheus
{
    public interface ICounter
    {
        void Inc(double increment = 1);
        double Value { get; }
    }

    public class Counter : Collector<Counter.Child>, ICounter
    {
        internal Counter(string name, string help, string[] labelNames, bool suppressInitialValue)
            : base(name, help, labelNames, suppressInitialValue)
        {
        }

        public void Inc(double increment = 1)
        {
            Unlabelled.Inc(increment);
        }

        public class Child : Prometheus.Child, ICounter
        {
            private ThreadSafeDouble _value;

            protected override void Populate(Metric metric)
            {
                metric.counter = new DataContracts.Counter();
                metric.counter.value = Value;
            }

            public void Inc(double increment = 1.0D)
            {
                // Note: Prometheus recommendations are that this assert > 0. However, there are times your
                // measurement results in a zero and it's easier to have the counter handle this elegantly.
                if (increment < 0.0D)
                    throw new ArgumentOutOfRangeException("increment", "Counter cannot go down");

                _value.Add(increment);
                _publish = true;
            }

            public double Value => _value.Value;
        }

        public double Value
        {
            get { return Unlabelled.Value; }
        }

        protected override MetricType Type
        {
            get { return MetricType.COUNTER; }
        }

        public void Publish() => Unlabelled.Publish();
    }
}