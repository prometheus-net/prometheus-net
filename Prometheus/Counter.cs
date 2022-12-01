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
            private ObservedExemplar _observedExemplar = ObservedExemplar.Empty;

            private protected override async Task CollectAndSerializeImplAsync(IMetricsSerializer serializer,
                CancellationToken cancel)
            {
                // Borrow the current exemplar
                ObservedExemplar cp =
                    Interlocked.CompareExchange(ref _observedExemplar, ObservedExemplar.Empty, _observedExemplar);

                await serializer.WriteMetricPointAsync(
                    Parent.NameBytes,
                    FlattenedLabelsBytes,
                    CanonicalLabel.Empty,
                    cancel,
                    Value,
                    cp);

                if (cp != ObservedExemplar.Empty)
                {
                    // attempt to return the exemplar to the pool unless a new one has arrived.
                    var prev = Interlocked.CompareExchange(ref _observedExemplar, cp, ObservedExemplar.Empty);
                    if (prev != ObservedExemplar.Empty) // a new exemplar is present so we return ours back to the pool. 
                        ObservedExemplar.ReturnPooled(cp);
                }
            }

            public void Inc(params Exemplar.LabelPair[] exemplar)
            {
                Inc(increment: 1, exemplar: exemplar);
            }

            public void Inc(double increment = 1.0, params Exemplar.LabelPair[] exemplar)
            {
                if (increment < 0.0)
                    throw new ArgumentOutOfRangeException(nameof(increment), "Counter value cannot decrease.");

                if (exemplar is { Length: > 0 })
                {
                    var ex = ObservedExemplar.CreatePooled(exemplar, increment);
                    var current = Interlocked.Exchange(ref _observedExemplar, ex);
                    if (current != ObservedExemplar.Empty)
                        ObservedExemplar.ReturnPooled(current);
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
}