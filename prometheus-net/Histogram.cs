using System;
using System.Diagnostics;
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

            if (_buckets.Length <= 1)
            {
                throw new ArgumentException("buckets length must be >= 1");
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
            private double _sum = 0;
            private ulong _count = 0;
            private ulong[] _bucketCounts;
            private readonly object _lock = new object();

            //re-using WireFormat.Histogram to avoid excessive array allocations 
            //TODO: this will break thread-safety if the very same histogram instance is registered on multiple CollectorRegistries
            //TODO: we should investigate if HdrHistogram can be a good candidate to use here internally - that's heavily optimized

            private Advanced.DataContracts.Histogram _wireMetric;
            private double[] _buckets;
            
            internal override void Init(ICollector parent, LabelValues labelValues)
            {
                base.Init(parent, labelValues);

                _buckets = ((Histogram) parent)._buckets;

                if (_buckets == null)
                {
                    return;
                }

                _bucketCounts = new ulong[_buckets.Length];
                _wireMetric = new Advanced.DataContracts.Histogram();
                for (int i = 0; i < _bucketCounts.Length; i++)
                {
                    _wireMetric.bucket.Add(new Bucket
                    {
                        upper_bound = _buckets[i]
                    });
                }
            }

            protected override void Populate(Metric metric)
            {
                lock (_lock)
                {
                    _wireMetric.bucket[0].cumulative_count = _bucketCounts[0];
                    for (int i = 1; i < _bucketCounts.Length; i++)
                    {
                        _wireMetric.bucket[i].cumulative_count = _bucketCounts[i] + _wireMetric.bucket[i - 1].cumulative_count;
                    }
                    _wireMetric.sample_count = _count;
                    _wireMetric.sample_sum = _sum;
                }

                metric.histogram = _wireMetric;

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
                    return;
                }

                lock (_lock)
                {
                    ++_count;
                    _sum += val;
                    _bucketCounts[bucketIndex] += 1;
                }
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