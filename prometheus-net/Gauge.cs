using System.Threading;
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


        public class Child : Advanced.Child, IGauge
        {
            private double _value;

            protected override void Populate(Metric metric)
            {
                metric.gauge = new Advanced.DataContracts.Gauge();
                metric.gauge.value = Value;
            }

            public void Inc(double increment = 1)
            {
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

            public void Set(double val)
            {
                Interlocked.Exchange(ref _value, val);
            }


            public void Dec(double decrement = 1)
            {
                Inc(-decrement);
            }

            public double Value
            {
                get
                {
                    return Interlocked.CompareExchange(ref _value, 0, 0);
                }
            }
        }

        protected override MetricType Type
        {
            get { return MetricType.GAUGE; }
        }

        public void Inc(double increment = 1)
        {
            Unlabelled.Inc(increment);
        }

        public void Set(double val)
        {
            Unlabelled.Set(val);
        }


        public void Dec(double decrement = 1)
        {
            Unlabelled.Dec(decrement);
        }

        public double Value
        {
            get { return Unlabelled.Value; }
        }
    }
}