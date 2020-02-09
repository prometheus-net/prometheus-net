using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        internal Histogram(string name, string help, string[]? labelNames, bool suppressInitialValue, double[]? buckets) : base(name, help, labelNames, suppressInitialValue)
        {
            if (labelNames?.Any(l => l == "le") == true)
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

        private protected override Child NewChild(Labels labels, bool publish)
        {
            return new Child(this, labels, publish);
        }

        public sealed class Child : ChildBase, IHistogram
        {
            internal Child(Histogram parent, Labels labels, bool publish)
                : base(parent, labels, publish)
            {
                _parent = parent;

                _upperBounds = _parent._buckets;
                _bucketCounts = new ThreadSafeLong[_upperBounds.Length];

                _sumIdentifier = CreateIdentifier("sum");
                _countIdentifier = CreateIdentifier("count");

                _bucketIdentifiers = new byte[_upperBounds.Length][];
                for (var i = 0; i < _upperBounds.Length; i++)
                {
                    var value = double.IsPositiveInfinity(_upperBounds[i]) ? "+Inf" : _upperBounds[i].ToString(CultureInfo.InvariantCulture);

                    _bucketIdentifiers[i] = CreateIdentifier("bucket", ("le", value));
                }
            }

            private readonly Histogram _parent;

            private ThreadSafeDouble _sum = new ThreadSafeDouble(0.0D);
            private readonly ThreadSafeLong[] _bucketCounts;
            private readonly double[] _upperBounds;

            internal readonly byte[] _sumIdentifier;
            internal readonly byte[] _countIdentifier;
            internal readonly byte[][] _bucketIdentifiers;

            private protected override async Task CollectAndSerializeImplAsync(IMetricsSerializer serializer, CancellationToken cancel)
            {
                // We output sum.
                // We output count.
                // We output each bucket in order of increasing upper bound.

                await serializer.WriteMetricAsync(_sumIdentifier, _sum.Value, cancel);
                await serializer.WriteMetricAsync(_countIdentifier, _bucketCounts.Sum(b => b.Value), cancel);

                var cumulativeCount = 0L;

                for (var i = 0; i < _bucketCounts.Length; i++)
                {
                    cumulativeCount += _bucketCounts[i].Value;

                    await serializer.WriteMetricAsync(_bucketIdentifiers[i], cumulativeCount, cancel);
                }
            }

            public double Sum => _sum.Value;
            public long Count => _bucketCounts.Sum(b => b.Value);

            public void Observe(double val) => Observe(val, 1);

            public void Observe(double val, long count)
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
                        break;
                    }
                }
                _sum.Add(val * count);
                Publish();
            }
        }

        private protected override MetricType Type => MetricType.Histogram;

        public double Sum => Unlabelled.Sum;
        public long Count => Unlabelled.Count;
        public void Observe(double val) => Unlabelled.Observe(val, 1);
        public void Observe(double val, long count) => Unlabelled.Observe(val, count);
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

            var buckets = new double[count];

            for (var i = 0; i < buckets.Length; i++)
            {
                buckets[i] = start;
                start *= factor;
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

            var buckets = new double[count];

            for (var i = 0; i < buckets.Length; i++)
            {
                buckets[i] = start;
                start += width;
            }

            return buckets;
        }
    }
}
