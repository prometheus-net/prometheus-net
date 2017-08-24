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


        public class Timer
        {
            private System.Diagnostics.Stopwatch _stopwatch;
            private Gauge.Child _child;

            public Timer(Gauge.Child child)
            {
                _child = child;
                _stopwatch = System.Diagnostics.Stopwatch.StartNew();
            }

            public void ApplyDuration()
            {
                _child.Set(_stopwatch.Elapsed.Seconds);
            }
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
                // Atomic increment
                double initalValue, computedValue; 
                do {
                    initalValue = _value;
                    computedValue = initalValue + increment;
                } while ( initalValue != Interlocked.CompareExchange(ref _value, computedValue, initalValue));
            }

            public void Set(double val)
            {
                // Atomic increment
                double initalValue, computedValue; 
                do {
                    initalValue = _value;
                    computedValue = val;
                } while ( initalValue != Interlocked.CompareExchange(ref _value, computedValue, initalValue));                
            }

            public void SetToCurrentTime()
            {
                var unixTicks = System.DateTime.UtcNow.Ticks - new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc).Ticks;
                Set(unixTicks / System.TimeSpan.TicksPerSecond);
            }

            public Gauge.Timer StartTimer()
            {
                return new Gauge.Timer(this);
            }

            public void Dec(double decrement = 1)
            {
                Inc(-decrement);
            }

            public double Value
            {
                get
                {
                    return _value;
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