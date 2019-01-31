using System;
using System.Linq;

namespace Prometheus
{
    public interface IHistogram : IObserver
    {
    }

    public sealed class Histogram : Collector<Histogram.Child>, IHistogram
    {
        private static readonly double[] DefaultBuckets = { .005, .01, .025, .05, .075, .1, .25, .5, .75, 1, 2.5, 5, 7.5, 10 };
        private readonly double[] _buckets;

        internal Histogram(string name, string help, string[] labelNames, bool suppressInitialValue, double[] buckets) : base(name, help, labelNames, suppressInitialValue)
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

        public sealed class Child : ChildBase, IHistogram
        {
            private ThreadSafeDouble _sum = new ThreadSafeDouble(0.0D);
            private ThreadSafeLong[] _bucketCounts;
            private double[] _upperBounds;

            internal override void Init(Collector parent, LabelValues labelValues, bool publish)
            {
                base.Init(parent, labelValues, publish);

                _upperBounds = ((Histogram)parent)._buckets;
                _bucketCounts = new ThreadSafeLong[_upperBounds.Length];
            }

            internal override void Populate(MetricData metric)
            {
                var wireMetric = new HistogramData();
                wireMetric.SampleCount = 0L;
                wireMetric.Buckets = new HistogramBucketData[_bucketCounts.Length];

                for (var i = 0; i < _bucketCounts.Length; i++)
                {
                    wireMetric.SampleCount += _bucketCounts[i].Value;
                    wireMetric.Buckets[i] = new HistogramBucketData
                    {
                        UpperBound = _upperBounds[i],
                        CumulativeCount = wireMetric.SampleCount
                    };
                }
                wireMetric.SampleSum = _sum.Value;

                metric.Histogram = wireMetric;
            }

            public void Observe(double val)
            {
                if (double.IsNaN(val))
                {
                    return;
                }

                for (int i = 0; i < _upperBounds.Length; i++)
                {
                    if (val <= _upperBounds[i])
                    {
                        _bucketCounts[i].Add(1);
                        break;
                    }
                }
                _sum.Add(val);
                _publish = true;
            }
        }

        internal override MetricType Type => MetricType.Histogram;

        public void Observe(double val) => Unlabelled.Observe(val);

        public void Publish() => Unlabelled.Publish();

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
