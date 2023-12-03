using System.Runtime.CompilerServices;

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

#if NET
        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
#endif
        private protected override async ValueTask CollectAndSerializeImplAsync(IMetricsSerializer serializer, CancellationToken cancel)
        {
            var exemplar = BorrowExemplar(ref _observedExemplar);

            await serializer.WriteMetricPointAsync(
                Parent.NameBytes,
                FlattenedLabelsBytes,
                CanonicalLabel.Empty,
                Value,
                exemplar,
                null,
                cancel);

            ReturnBorrowedExemplar(ref _observedExemplar, exemplar);
        }

        public void Inc(double increment = 1.0)
        {
            Inc(increment: increment, null);
        }

        public void Inc(Exemplar? exemplar)
        {
            Inc(increment: 1, exemplar: exemplar);
        }

        public void Inc(double increment, Exemplar? exemplar)
        {
            if (increment < 0.0)
                throw new ArgumentOutOfRangeException(nameof(increment), "Counter value cannot decrease.");

            exemplar ??= GetDefaultExemplar(increment);

            if (exemplar?.Length > 0)
                RecordExemplar(exemplar, ref _observedExemplar, increment);

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

    public void Inc(double increment = 1.0) => Unlabelled.Inc(increment);
    public void IncTo(double targetValue) => Unlabelled.IncTo(targetValue);
    public double Value => Unlabelled.Value;

    public void Publish() => Unlabelled.Publish();
    public void Unpublish() => Unlabelled.Unpublish();

    public void Inc(Exemplar? exemplar) => Inc(increment: 1, exemplar: exemplar);
    public void Inc(double increment, Exemplar? exemplar) => Unlabelled.Inc(increment, exemplar);

    internal override MetricType Type => MetricType.Counter;

    internal override int TimeseriesCount => ChildCount;
}