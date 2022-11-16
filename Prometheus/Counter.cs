namespace Prometheus
{
    public sealed class Counter : Collector<Counter.Child>, ICounter
    {
        public sealed class Child : ChildBase, ICounter
        {
            internal Child(Collector parent, LabelSequence instanceLabels, LabelSequence flattenedLabels, bool publish)
                : base(parent, instanceLabels, flattenedLabels, publish)
            {
            }

            private ThreadSafeDouble _value;

            private protected override async Task CollectAndSerializeImplAsync(IMetricsSerializer serializer, CancellationToken cancel)
            {
                await serializer.WriteMetricPointAsync(
                    Parent.NameBytes,
                    FlattenedLabelsBytes,
                    CanonicalLabel.Empty,
                    cancel, 
                    Value);
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

        private protected override Child NewChild(LabelSequence instanceLabels, LabelSequence flattenedLabels, bool publish)
        {
            return new Child(this, instanceLabels, flattenedLabels, publish);
        }

        internal Counter(string name, string help, StringSequence instanceLabelNames, LabelSequence staticLabels, bool suppressInitialValue)
            : base(name, help, instanceLabelNames, staticLabels, suppressInitialValue)
        {
        }

        public void Inc(double increment = 1) => Unlabelled.Inc(increment);
        public void IncTo(double targetValue) => Unlabelled.IncTo(targetValue);
        public double Value => Unlabelled.Value;

        public void Publish() => Unlabelled.Publish();
        public void Unpublish() => Unlabelled.Unpublish();

        internal override MetricType Type => MetricType.Counter;

        internal override int TimeseriesCount => ChildCount;
    }
}