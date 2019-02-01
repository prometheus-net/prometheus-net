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
            internal Child(Collector parent, Labels labels, bool publish)
                : base(parent, labels, publish)
            {
                _identifier = CreateIdentifier();
            }

            private readonly string _identifier;

            private ThreadSafeDouble _value;

            internal override void CollectAndSerializeImpl(IMetricsSerializer serializer)
            {
                serializer.WriteMetric(_identifier, Value);
            }

            public void Inc(double increment = 1.0)
            {
                // Note: Prometheus recommendations are that this assert > 0. However, there are times your
                // measurement results in a zero and it's easier to have the counter handle this elegantly.
                if (increment < 0.0)
                    throw new ArgumentOutOfRangeException("increment", "Counter cannot go down");

                _value.Add(increment);
                Publish();
            }

            public double Value => _value.Value;
        }

        internal override Child NewChild(Labels labels, bool publish)
        {
            return new Child(this, labels, publish);
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