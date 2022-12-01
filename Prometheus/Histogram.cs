using System.Globalization;

namespace Prometheus
{
    /// <remarks>
    /// The histogram is thread-safe but not atomic - the sum of values and total count of events
    /// may not add up perfectly with bucket contents if new observations are made during a collection.
    /// </remarks>
    public sealed class Histogram : Collector<Histogram.Child>, IHistogram
    {
        private static readonly double[] DefaultBuckets = { .005, .01, .025, .05, .075, .1, .25, .5, .75, 1, 2.5, 5, 7.5, 10 };
        private readonly double[] _buckets;

        internal Histogram(string name, string help, StringSequence instanceLabelNames, LabelSequence staticLabels, bool suppressInitialValue, double[]? buckets)
            : base(name, help, instanceLabelNames, staticLabels, suppressInitialValue)
        {
            if (instanceLabelNames.Contains("le"))
            {
                throw new ArgumentException("'le' is a reserved label name");
            }
            _buckets = buckets ?? DefaultBuckets;

            if (_buckets.Length == 0)
            {
                throw new ArgumentException("Histogram must have at least one bucket");
            }

            if (!double.IsPositiveInfinity(_buckets[_buckets.Length - 1]))
            {
                _buckets = _buckets.Concat(new[] { double.PositiveInfinity }).ToArray();
            }

            for (int i = 1; i < _buckets.Length; i++)
            {
                if (_buckets[i] <= _buckets[i - 1])
                {
                    throw new ArgumentException("Bucket values must be increasing");
                }
            }
        }

        private protected override Child NewChild(LabelSequence instanceLabels, LabelSequence flattenedLabels, bool publish)
        {
            return new Child(this, instanceLabels, flattenedLabels, publish);
        }

        public sealed class Child : ChildBase, IHistogram
        {
            internal Child(Histogram parent, LabelSequence instanceLabels, LabelSequence flattenedLabels, bool publish)
                : base(parent, instanceLabels, flattenedLabels, publish)
            {
                Parent = parent;
                
                _upperBounds = Parent._buckets;
                _bucketCounts = new ThreadSafeLong[_upperBounds.Length];
                _leLabels = new CanonicalLabel[_upperBounds.Length];
                for (var i = 0; i < Parent._buckets.Length; i++)
                {
                    _leLabels[i] = TextSerializer.EncodeValueAsCanonicalLabel(LeLabelName, Parent._buckets[i]);
                }
                _exemplars = new ObservedExemplar[_upperBounds.Length];
                for (var i = 0; i < _upperBounds.Length; i++)
                {
                    _exemplars[i] = ObservedExemplar.Empty;
                }
            }

            internal new readonly Histogram Parent;

            private ThreadSafeDouble _sum = new ThreadSafeDouble(0.0D);
            private readonly ThreadSafeLong[] _bucketCounts;
            private readonly double[] _upperBounds;
            private readonly CanonicalLabel[] _leLabels;
            private static readonly byte[] SumSuffix = PrometheusConstants.ExportEncoding.GetBytes("sum");
            private static readonly byte[] CountSuffix = PrometheusConstants.ExportEncoding.GetBytes("count");
            private static readonly byte[] BucketSuffix = PrometheusConstants.ExportEncoding.GetBytes("bucket");
            private static readonly byte[] LeLabelName = PrometheusConstants.ExportEncoding.GetBytes("le");
            private readonly ObservedExemplar[] _exemplars;

            private protected override async Task CollectAndSerializeImplAsync(IMetricsSerializer serializer,
                CancellationToken cancel)
            {
                // We output sum.
                // We output count.
                // We output each bucket in order of increasing upper bound.
                await serializer.WriteMetricPointAsync(
                    Parent.NameBytes,
                    FlattenedLabelsBytes,
                    CanonicalLabel.Empty,
                    cancel,
                    _sum.Value,
                    ObservedExemplar.Empty,
                    suffix: SumSuffix);
                await serializer.WriteMetricPointAsync(
                    Parent.NameBytes,
                    FlattenedLabelsBytes,
                    CanonicalLabel.Empty,
                    cancel,
                    _bucketCounts.Sum(b => b.Value),
                    ObservedExemplar.Empty,
                    suffix: CountSuffix);

                var cumulativeCount = 0L;

                for (var i = 0; i < _bucketCounts.Length; i++)
                {
                    // borrow the current exemplar.
                    ObservedExemplar cp = Interlocked.CompareExchange(ref _exemplars[i], ObservedExemplar.Empty, _exemplars[i]);
                    cumulativeCount += _bucketCounts[i].Value;
                    await serializer.WriteMetricPointAsync(
                        Parent.NameBytes,
                        FlattenedLabelsBytes,
                        _leLabels[i],
                        cancel,
                        cumulativeCount,
                        cp,
                        suffix: BucketSuffix);
                    
                    if (cp != ObservedExemplar.Empty)
                    {
                        // attempt to return the exemplar to the pool unless a new one has arrived.
                        var prev = Interlocked.CompareExchange(ref _exemplars[i], cp, ObservedExemplar.Empty);
                        if (prev != ObservedExemplar.Empty) // a new exemplar is present so we return ours back to the pool. 
                            ObservedExemplar.ReturnPooled(cp);
                    }
                }
            }

            public double Sum => _sum.Value;
            public long Count => _bucketCounts.Sum(b => b.Value);

            public void Observe(double val, params Exemplar.LabelPair[] exemplar) => ObserveInternal(val, 1, exemplar);

            public void Observe(double val) => Observe(val, 1);

            public void Observe(double val, long count) => ObserveInternal(val, count);

            private void ObserveInternal(double val, long count, params Exemplar.LabelPair[] exemplar)
            {
                if (double.IsNaN(val))
                {
                    return;
                }

                for (int i = 0; i < _upperBounds.Length; i++)
                {
                    if (val <= _upperBounds[i])
                    {
                        _bucketCounts[i].Add(count);
                        if (exemplar is { Length: > 0 })
                        {
                            var observedExemplar = ObservedExemplar.CreatePooled(exemplar, val);
                            var current = Interlocked.Exchange(ref _exemplars[i], observedExemplar);
                            if (current != ObservedExemplar.Empty)
                                ObservedExemplar.ReturnPooled(current);
                        }

                        break;
                    }
                }
                _sum.Add(val * count);
                Publish();
            }
        }

        internal override MetricType Type => MetricType.Histogram;

        public double Sum => Unlabelled.Sum;
        public long Count => Unlabelled.Count;
        public void Observe(double val) => Unlabelled.Observe(val, 1);
        public void Observe(double val, long count) => Unlabelled.Observe(val, count);
        public void Observe(double val, params Exemplar.LabelPair[] exemplar) => Unlabelled.Observe(val, exemplar);
        public void Publish() => Unlabelled.Publish();
        public void Unpublish() => Unlabelled.Unpublish();

        // From https://github.com/prometheus/client_golang/blob/master/prometheus/histogram.go
        /// <summary>  
        ///  Creates '<paramref name="count"/>' buckets, where the lowest bucket has an
        ///  upper bound of '<paramref name="start"/>' and each following bucket's upper bound is '<paramref name="factor"/>'
        ///  times the previous bucket's upper bound.
        /// 
        ///  The function throws if '<paramref name="count"/>' is 0 or negative, if '<paramref name="start"/>' is 0 or negative,
        ///  or if '<paramref name="factor"/>' is less than or equal 1.
        /// </summary>
        /// <param name="start">The upper bound of the lowest bucket. Must be positive.</param>
        /// <param name="factor">The factor to increase the upper bound of subsequent buckets. Must be greater than 1.</param>
        /// <param name="count">The number of buckets to create. Must be positive.</param>
        public static double[] ExponentialBuckets(double start, double factor, int count)
        {
            if (count <= 0) throw new ArgumentException($"{nameof(ExponentialBuckets)} needs a positive {nameof(count)}");
            if (start <= 0) throw new ArgumentException($"{nameof(ExponentialBuckets)} needs a positive {nameof(start)}");
            if (factor <= 1) throw new ArgumentException($"{nameof(ExponentialBuckets)} needs a {nameof(factor)} greater than 1");

            // The math we do can make it incur some tiny avoidable error due to floating point gremlins.
            // We use decimal for the path to preserve as much accuracy as we can, before finally converting to double.
            // It will not fix 100% of the cases where we end up with 0.0000000000000000000000000000001 offset but it helps a lot.

            var next = (decimal)start;
            var buckets = new double[count];

            for (var i = 0; i < buckets.Length; i++)
            {
                buckets[i] = (double)next;
                next *= (decimal)factor;
            }

            return buckets;
        }

        // From https://github.com/prometheus/client_golang/blob/master/prometheus/histogram.go
        /// <summary>  
        ///  Creates '<paramref name="count"/>' buckets, where the lowest bucket has an
        ///  upper bound of '<paramref name="start"/>' and each following bucket's upper bound is the upper bound of the
        ///  previous bucket, incremented by '<paramref name="width"/>'
        /// 
        ///  The function throws if '<paramref name="count"/>' is 0 or negative.
        /// </summary>
        /// <param name="start">The upper bound of the lowest bucket.</param>
        /// <param name="width">The width of each bucket (distance between lower and upper bound).</param>
        /// <param name="count">The number of buckets to create. Must be positive.</param>
        public static double[] LinearBuckets(double start, double width, int count)
        {
            if (count <= 0) throw new ArgumentException($"{nameof(LinearBuckets)} needs a positive {nameof(count)}");

            // The math we do can make it incur some tiny avoidable error due to floating point gremlins.
            // We use decimal for the path to preserve as much accuracy as we can, before finally converting to double.
            // It will not fix 100% of the cases where we end up with 0.0000000000000000000000000000001 offset but it helps a lot.

            var next = (decimal)start;
            var buckets = new double[count];

            for (var i = 0; i < buckets.Length; i++)
            {
                buckets[i] = (double)next;
                next += (decimal)width;
            }

            return buckets;
        }

        /// <summary>
        /// Divides each power of 10 into N divisions.
        /// </summary>
        /// <param name="startPower">The starting range includes 10 raised to this power.</param>
        /// <param name="endPower">The ranges end with 10 raised to this power (this no longer starts a new range).</param>
        /// <param name="divisions">How many divisions to divide each range into.</param>
        /// <remarks>
        /// For example, with startPower=-1, endPower=2, divisions=4 we would get:
        /// 10^-1 == 0.1 which defines our starting range, giving buckets: 0.25, 0.5, 0.75, 1.0
        /// 10^0 == 1 which is the next range, giving buckets: 2.5, 5, 7.5, 10
        /// 10^1 == 10 which is the next range, giving buckets: 25, 50, 75, 100
        /// 10^2 == 100 which is the end and the top level of the preceding range.
        /// Giving total buckets: 0.25, 0.5, 0.75, 1.0, 2.5, 5, 7.5, 10, 25, 50, 75, 100
        /// </remarks>
        public static double[] PowersOfTenDividedBuckets(int startPower, int endPower, int divisions)
        {
            if (startPower >= endPower)
                throw new ArgumentException($"{nameof(startPower)} must be less than {nameof(endPower)}.", nameof(startPower));

            if (divisions <= 0)
                throw new ArgumentOutOfRangeException($"{nameof(divisions)} must be a positive integer.", nameof(divisions));

            var buckets = new List<double>();

            for (var powerOfTen = startPower; powerOfTen < endPower; powerOfTen++)
            {
                // This gives us the upper bound (the start of the next range).
                var max = (decimal)Math.Pow(10, powerOfTen + 1);

                // Then we just divide it into N divisions and we are done!
                for (var division = 0; division < divisions; division++)
                {
                    var bucket = max / divisions * (division + 1);

                    // The math we do can make it incur some tiny avoidable error due to floating point gremlins.
                    // We use decimal for the path to preserve as much accuracy as we can, before finally converting to double.
                    // It will not fix 100% of the cases where we end up with 0.0000000000000000000000000000001 offset but it helps a lot.
                    var candidate = (double)bucket;

                    // Depending on the number of divisions, it may be that divisions from different powers overlap.
                    // For example, a division into 20 would include:
                    // 19th value in the 0th power: 9.5 (10/20*19=9.5)
                    // 1st value in the 1st power: 5 (100/20*1 = 5)
                    // To avoid this being a problem, we simply constrain all values to be increasing.
                    if (buckets.Any() && buckets.Last() >= candidate)
                        continue; // Skip this one, it is not greater.

                    buckets.Add(candidate);
                }
            }

            return buckets.ToArray();
        }

        // sum + count + buckets
        internal override int TimeseriesCount => ChildCount * (2 + _buckets.Length);
    }
}