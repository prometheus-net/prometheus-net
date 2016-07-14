using System;
using System.Threading;
using Prometheus.Advanced;
using Prometheus.Advanced.DataContracts;

namespace Prometheus
{
    public interface ICounter
    {
        void Inc(double increment = 1);
        double Value { get; }
    }

    public class Counter : Collector<Counter.Child>, ICounter
    {

        internal Counter(string name, string help, string[] labelNames)
            : base(name, help, labelNames)
        {
        }

        public void Inc(double increment = 1)
        {
            Unlabelled.Inc(increment);
        }

        public class Child : Advanced.Child, ICounter
        {
            private double _value;

            protected override void Populate(Metric metric)
            {
                metric.counter = new Advanced.DataContracts.Counter();
                metric.counter.value = Value;
            }

            public void Inc(double increment = 1)
            {
                if (increment < 0)
                {
                    throw new InvalidOperationException("Counter cannot go down");
                }

                double newCurrentValue = 0;
                while (true)
                {
                    double currentValue = newCurrentValue;
                    double newValue = currentValue + increment;
                    newCurrentValue = Interlocked.CompareExchange(ref _value, newValue, currentValue);
                    if (newCurrentValue == currentValue)
                        return;
                }
            }

            public double Value
            {
                get
                {
                    return Interlocked.CompareExchange(ref _value, 0, 0);
                }
            }
        }

        public double Value
        {
            get { return Unlabelled.Value; }
        }

        protected override MetricType Type
        {
            get { return MetricType.COUNTER; }
        }
    }
}