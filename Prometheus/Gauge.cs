namespace Prometheus
{
    public sealed class Gauge : Collector<Gauge.Child>, IGauge
    {
        public sealed class Child : ChildBase, IGauge
        {
            internal Child(Collector parent, LabelSequence instanceLabels, LabelSequence flattenedLabels, bool publish)
                : base(parent, instanceLabels, flattenedLabels, publish)
            {
            }

            private ThreadSafeDouble _value;

            private protected override async Task CollectAndSerializeImplAsync(IMetricsSerializer serializer, CancellationToken cancel)
            {
                await serializer.WriteIdentifierPartAsync(_parent.NameBytes,FlattenedLabelsBytes, cancel);
                await serializer.WriteValuePartAsync(Value, cancel);
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

            public void IncTo(double targetValue)
            {
                _value.IncrementTo(targetValue);
                Publish();
            }

            public void DecTo(double targetValue)
            {
                _value.DecrementTo(targetValue);
                Publish();
            }

            public double Value => _value.Value;
        }

        private protected override Child NewChild(LabelSequence instanceLabels, LabelSequence flattenedLabels, bool publish)
        {
            return new Child(this, instanceLabels, flattenedLabels, publish);
        }

        internal Gauge(string name, string help, StringSequence instanceLabelNames, LabelSequence staticLabels, bool suppressInitialValue)
            : base(name, help, instanceLabelNames, staticLabels, suppressInitialValue)
        {
        }

        public void Inc(double increment = 1) => Unlabelled.Inc(increment);
        public void Set(double val) => Unlabelled.Set(val);
        public void Dec(double decrement = 1) => Unlabelled.Dec(decrement);
        public void IncTo(double targetValue) => Unlabelled.IncTo(targetValue);
        public void DecTo(double targetValue) => Unlabelled.DecTo(targetValue);
        public double Value => Unlabelled.Value;
        public void Publish() => Unlabelled.Publish();
        public void Unpublish() => Unlabelled.Unpublish();

        internal override MetricType Type => MetricType.Gauge;

        internal override int TimeseriesCount => ChildCount;
    }
}