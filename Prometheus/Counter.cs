namespace Prometheus;

public sealed class Counter : Collector<Counter.Child>, ICounter
{
    public sealed class Child : ChildBase, ICounter
    {
        internal Child(Collector parent, LabelSequence instanceLabels, LabelSequence flattenedLabels, bool publish, ExemplarBehavior exemplarBehavior)
            : base(parent, instanceLabels, flattenedLabels, publish, exemplarBehavior)
        {
        }

        private ThreadSafeDouble _value;
        private ObservedExemplar _observedExemplar = ObservedExemplar.Empty;

        private protected override async Task CollectAndSerializeImplAsync(IMetricsSerializer serializer, CancellationToken cancel)
        {
            var exemplar = BorrowExemplar(ref _observedExemplar);

            await serializer.WriteMetricPointAsync(
                Parent.NameBytes,
                FlattenedLabelsBytes,
                CanonicalLabel.Empty,
                cancel,
                Value,
                exemplar);

            ReturnBorrowedExemplar(ref _observedExemplar, exemplar);
        }

        public void Inc(double increment)
        {
            Inc(increment: increment, Exemplar.None);
        }

        public void Inc(Exemplar? exemplar)
        {
            Inc(increment: 1, exemplar: exemplar);
        }

        public void Inc(double increment = 1.0, Exemplar? exemplar = null)
        {
            if (increment < 0.0)
                throw new ArgumentOutOfRangeException(nameof(increment), "Counter value cannot decrease.");

            if (!exemplar.HasValue)
                exemplar = GetDefaultExemplar(increment);

            if (exemplar.HasValue)
                RecordExemplar(exemplar.Value, ref _observedExemplar, increment);

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


    private protected override Child NewChild(LabelSequence instanceLabels, LabelSequence flattenedLabels, bool publish, ExemplarBehavior exemplarBehavior)
    {
        return new Child(this, instanceLabels, flattenedLabels, publish, exemplarBehavior);
    }

    internal Counter(string name, string help, StringSequence instanceLabelNames, LabelSequence staticLabels, bool suppressInitialValue, ExemplarBehavior exemplarBehavior)
        : base(name, help, instanceLabelNames, staticLabels, suppressInitialValue, exemplarBehavior)
    {
    }

    public void Inc(double increment) => Unlabelled.Inc(increment);
    public void IncTo(double targetValue) => Unlabelled.IncTo(targetValue);
    public double Value => Unlabelled.Value;

    public void Publish() => Unlabelled.Publish();
    public void Unpublish() => Unlabelled.Unpublish();

    public void Inc(Exemplar? exemplar)
    {
        Inc(increment: 1, exemplar: exemplar);
    }

    public void Inc(double increment = 1, Exemplar? exemplar = null) =>
        Unlabelled.Inc(increment, exemplar);

    internal override MetricType Type => MetricType.Counter;

    internal override int TimeseriesCount => ChildCount;
}