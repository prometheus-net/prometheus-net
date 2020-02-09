using System;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus
{
    public sealed class Counter : Collector<Counter.Child>, ICounter
    {
        public sealed class Child : ChildBase, ICounter
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

            public void Inc(double increment = 1.0)
            {
                if (increment < 0.0)
                    throw new ArgumentOutOfRangeException(nameof(increment), "Counter value cannot decrease.");

                _value.Add(increment);
                Publish();
            }

            public void IncTo(double targetValue)
            {
                _value.IncrementTo(targetValue);
                Publish();
            }

            public double Value => _value.Value;
        }

        private protected override Child NewChild(Labels labels, bool publish)
        {
            return new Child(this, labels, publish);
        }

        internal Counter(string name, string help, string[]? labelNames, bool suppressInitialValue)
            : base(name, help, labelNames, suppressInitialValue)
        {
        }

        public void Inc(double increment = 1) => Unlabelled.Inc(increment);
        public void IncTo(double targetValue) => Unlabelled.IncTo(targetValue);
        public double Value => Unlabelled.Value;

        public void Publish() => Unlabelled.Publish();
        public void Unpublish() => Unlabelled.Unpublish();

        private protected override MetricType Type => MetricType.Counter;
    }
}