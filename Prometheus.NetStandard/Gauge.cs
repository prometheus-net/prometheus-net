using System.Threading;
using System.Threading.Tasks;

namespace Prometheus
{
    public sealed class Gauge : Collector<Gauge.Child>, IGauge
    {
        public sealed class Child : ChildBase, IGauge
        {
            internal Child(Collector parent, Labels labels, bool publish)
                : base(parent, labels, publish)
            {
                _identifier = CreateIdentifier();
            }

            private readonly byte[] _identifier;

            private ThreadSafeDouble _value;

            private protected override Task CollectAndSerializeImplAsync(IMetricsSerializer serializer, CancellationToken cancel)
            {
                return serializer.WriteMetricAsync(_identifier, Value, cancel);
            }

            public void Inc(double increment = 1)
            {
                _value.Add(increment);
                Publish();
            }

            public void Set(double val)
            {
                _value.Value = val;
                Publish();
            }

            public void Dec(double decrement = 1)
            {
                Inc(-decrement);
            }

            public double Value => _value.Value;
        }

        private protected override Child NewChild(Labels labels, bool publish)
        {
            return new Child(this, labels, publish);
        }

        internal Gauge(string name, string help, string[]? labelNames, bool suppressInitialValue)
            : base(name, help, labelNames, suppressInitialValue)
        {
        }

        public void Inc(double increment = 1) => Unlabelled.Inc(increment);
        public void Set(double val) => Unlabelled.Set(val);
        public void Dec(double decrement = 1) => Unlabelled.Dec(decrement);
        public double Value => Unlabelled.Value;

        public void Publish() => Unlabelled.Publish();

        private protected override MetricType Type => MetricType.Gauge;
    }
}