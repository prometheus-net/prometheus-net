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
            private readonly object _lock = new object();

            protected override void Populate(Metric metric)
            {
                metric.gauge = new Advanced.DataContracts.Gauge();
                lock (_lock)
                {
                    metric.gauge.value = _value;
                }
            }

            public void Inc(double increment = 1)
            {
                lock (_lock)
                {
                    _value += increment;
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
                    lock (_lock)
                    {
                        return _value;
                    }
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