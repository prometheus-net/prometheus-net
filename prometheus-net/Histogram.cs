using System;
using System.Diagnostics;
using System.Linq;
using Prometheus.Internal;

namespace Prometheus
{
    public class Histogram : Metric
    {
        private static readonly double[] DefaultBuckets = { .005, .01, .025, .05, .075, .1, .25, .5, .75, 1, 2.5, 5, 7.5, 10 };
        private readonly double[] _buckets;
        private readonly ulong[] _bucketCounts;
        private double _sum = 0;
        private ulong _count = 0;
        private readonly object _lock = new object();
        private readonly io.prometheus.client.Histogram _wireMetric;

        internal Histogram(MetricFamily family, LabelValues labelValues, double[] buckets = null)
            : base(family, labelValues)
        {
            _buckets = buckets ?? DefaultBuckets;

            if (_buckets.Length <= 1)
            {
                throw new ArgumentOutOfRangeException("buckets length must be >= 1");
            }

            if (!double.IsPositiveInfinity(_buckets[_buckets.Length-1]))
            {
                _buckets = _buckets.Concat(new[] { double.PositiveInfinity }).ToArray();
            }

            for (int i = 1; i < _buckets.Length; i++)
            {
                if (_buckets[i]<=_buckets[i-1])
                {
                    throw new ArgumentException("Bucket values must be increasing");
                }
            }

            _bucketCounts = new ulong[_buckets.Length];
            _wireMetric = new io.prometheus.client.Histogram();
            for (int i = 0; i < _bucketCounts.Length; i++)
            {
                _wireMetric.bucket.Add(new io.prometheus.client.Bucket()
                {
                    upper_bound = _buckets[i]
                });
            }
        }

        public void Observe(double val)
        {
            if (double.IsNaN(val))
            {
                return;
            }

            int bucketIndex = -1;
            for (int i = 0; i < _buckets.Length; i++)
            {
                if (val <= _buckets[i])
                {
                    bucketIndex = i;
                    break;
                }
            }

            if (bucketIndex == -1)
            {
                Trace.WriteLine(string.Format("Couldn't find a bucket for {0} in metric {1}", val, ToString()));
                return;
            }

            lock (_lock)
            {
                ++_count;
                _sum += val;
                _bucketCounts[bucketIndex] += 1;
            }
        }

        public Histogram Labels(params string[] labelValues)
        {
            return (Histogram) Family.GetOrAdd(labelValues, (family, values) => new Histogram(Family, values, _buckets));
        }

        internal override io.prometheus.client.MetricType Type
        {
            get { return io.prometheus.client.MetricType.HISTOGRAM; }
        }

        protected override void Populate(io.prometheus.client.Metric metric)
        {
            lock (_lock)
            {
                for (int i = 0; i < _bucketCounts.Length; i++)
                {
                    _wireMetric.bucket[i].cumulative_count = _bucketCounts[i] + (i > 0 ? _wireMetric.bucket[i-1].cumulative_count : 0);
                }
                _wireMetric.sample_count = _count;
                _wireMetric.sample_sum = _sum;
            }

            metric.histogram = _wireMetric;

        }
    }
}