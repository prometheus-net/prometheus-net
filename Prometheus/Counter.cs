namespace Prometheus;

public sealed class Counter : Collector<Counter.Child>, ICounter
{
    public sealed class Child : ChildBase, ICounter
    {
        internal Child(Collector parent, LabelSequence instanceLabels, LabelSequence flattenedLabels, bool publish)
            : base(parent, instanceLabels, flattenedLabels, publish)
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

        public void Inc(params Exemplar.LabelPair[] exemplarLabels)
        {
            Inc(increment: 1, exemplarLabels: exemplarLabels);
        }

        public void Inc(double increment = 1.0, params Exemplar.LabelPair[] exemplarLabels)
        {
            if (increment < 0.0)
                throw new ArgumentOutOfRangeException(nameof(increment), "Counter value cannot decrease.");

            exemplarLabels = ExemplarOrDefault(exemplarLabels);

            if (exemplarLabels is { Length: > 0 })
            {
                var exemplar = ObservedExemplar.CreatePooled(exemplarLabels, increment);
                ObservedExemplar.ReturnPooledIfNotEmpty(Interlocked.Exchange(ref _observedExemplar, exemplar));
            }

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

    public void Inc(params Exemplar.LabelPair[] exemplar)
    {
        Inc(increment: 1, exemplar: exemplar);
    }

    public void Inc(double increment = 1, params Exemplar.LabelPair[] exemplar) =>
        Unlabelled.Inc(increment, exemplar);

    internal override MetricType Type => MetricType.Counter;

    internal override int TimeseriesCount => ChildCount;
}