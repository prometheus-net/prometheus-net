using System;
using System.Linq;
using Prometheus.Advanced;
using Prometheus.Advanced.DataContracts;
using Prometheus.Internal;

namespace Prometheus
{
    public interface IHistogram
    {
        void Observe(double val);
    }

    public class Histogram : Collector<Histogram.Child>, IHistogram
    {
        private static readonly double[] DefaultBuckets = { .005, .01, .025, .05, .075, .1, .25, .5, .75, 1, 2.5, 5, 7.5, 10 };
        private readonly double[] _buckets;

        internal Histogram(string name, string help, string[] labelNames, double[] buckets = null) : base(name, help, labelNames)
        {
            if (labelNames.Any(l=>l == "le"))
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

            Unlabelled.Init(this, LabelValues.Empty);
        }

        public class Child : Advanced.Child, IHistogram
        {
            private ThreadSafeDouble _sum = new ThreadSafeDouble(0.0D);
            private ThreadSafeLong[] _bucketCounts;
            private double[] _upperBounds;
            
            internal override void Init(ICollector parent, LabelValues labelValues)
            {
                base.Init(parent, labelValues);

                _upperBounds = ((Histogram)parent)._buckets;
                _bucketCounts = new ThreadSafeLong[_upperBounds.Length];
            }

            protected override void Populate(Metric metric)
            {
                var wireMetric = new Advanced.DataContracts.Histogram();
                wireMetric.sample_count = 0L;

                for (var i = 0; i < _bucketCounts.Length; i++)
                {
                    wireMetric.sample_count += (ulong)_bucketCounts[i].Value;
                    wireMetric.bucket.Add(new Bucket
                    {
                        upper_bound = _upperBounds[i],
                        cumulative_count = wireMetric.sample_count
                    });
                }
                wireMetric.sample_sum = _sum.Value;

                metric.histogram = wireMetric;
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
            }
        }

        protected override MetricType Type
        {
            get { return MetricType.HISTOGRAM; }
        }

        public void Observe(double val)
        {
            Unlabelled.Observe(val);
        }
    }
}