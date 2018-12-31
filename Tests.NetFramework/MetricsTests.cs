using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus.Advanced;
using Prometheus.Advanced.DataContracts;
using System;
using System.Linq;

namespace Prometheus.Tests
{
    [TestClass]
    public class MetricsTests
    {
        public MetricsTests()
        {
            DefaultCollectorRegistry.Instance.Clear();
        }

        [TestMethod]
        public void api_usage()
        {
            var gauge = Metrics.CreateGauge("name1", "help1");
            gauge.Inc();
            Assert.AreEqual(1, gauge.Value);
            gauge.Inc(3.2);
            Assert.AreEqual(4.2, gauge.Value);
            gauge.Set(4);
            Assert.AreEqual(4, gauge.Value);
            gauge.Dec(0.2);
            Assert.AreEqual(3.8, gauge.Value);

            Assert.ThrowsException<ArgumentException>(() => gauge.Labels("1"));

            var counter = Metrics.CreateCounter("name2", "help2", "label1");
            counter.Inc();
            counter.Inc(3.2);
            counter.Inc(0);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => counter.Inc(-1));
            Assert.AreEqual(4.2, counter.Value);

            Assert.AreEqual(0, counter.Labels("a").Value);
            counter.Labels("a").Inc(3.3);
            counter.Labels("a").Inc(1.1);
            Assert.AreEqual(4.4, counter.Labels("a").Value);
        }

        [TestMethod]
        public void counter_collection()
        {
            var counter = Metrics.CreateCounter("name1", "help1", "label1");

            counter.Inc();
            counter.Inc(3.2);
            counter.Labels("abc").Inc(3.2);

            MetricFamily[] exported = DefaultCollectorRegistry.Instance.CollectAll().ToArray();

            Assert.AreEqual(1, exported.Length);
            var familiy1 = exported[0];
            Assert.AreEqual("name1", familiy1.name);
            Assert.AreEqual("help1", familiy1.help);
            var metrics = familiy1.metric;
            Assert.AreEqual(2, metrics.Count);

            // We need to sort the metrics as the order they are returned in is not fixed.
            // Let's just sort by counter value, descending (arbitrarily).
            metrics.Sort((a, b) => -a.counter.value.CompareTo(b.counter.value));

            foreach (var metric in metrics)
            {
                Assert.IsNull(metric.gauge);
                Assert.IsNull(metric.histogram);
                Assert.IsNull(metric.summary);
                Assert.IsNull(metric.untyped);
                Assert.IsNotNull(metric.counter);
            }

            Assert.AreEqual(4.2, metrics[0].counter.value);
            Assert.AreEqual(0, metrics[0].label.Count);

            Assert.AreEqual(3.2, metrics[1].counter.value);
            var labelPairs = metrics[1].label;
            Assert.AreEqual(1, labelPairs.Count);
            Assert.AreEqual("label1", labelPairs[0].name);
            Assert.AreEqual("abc", labelPairs[0].value);
        }

        [TestMethod]
        public void custom_registry()
        {
            var myRegistry = new DefaultCollectorRegistry();
            var counter1 = Metrics.WithCustomRegistry(myRegistry).CreateCounter("counter1", "help1"); //registered on a custom registry

            var counter2 = Metrics.CreateCounter("counter1", "help1"); //created on different registry - same name is hence permitted

            counter1.Inc(3);
            counter2.Inc(4);

            Assert.AreEqual(3, myRegistry.CollectAll().ToArray()[0].metric[0].counter.value); //counter1 == 3
            Assert.AreEqual(4, DefaultCollectorRegistry.Instance.CollectAll().ToArray()[0].metric[0].counter.value); //counter2 == 4
        }

        [TestMethod]
        public void gauge_collection()
        {
            var gauge = Metrics.CreateGauge("name1", "help1");

            gauge.Inc();
            gauge.Inc(3.2);
            gauge.Set(4);
            gauge.Dec(0.2);

            var exported = DefaultCollectorRegistry.Instance.CollectAll().ToArray();

            Assert.AreEqual(1, exported.Length);
            var familiy1 = exported[0];
            Assert.AreEqual("name1", familiy1.name);
            Assert.AreEqual("help1", familiy1.help);
            var metrics = familiy1.metric;
            Assert.AreEqual(1, metrics.Count);

            foreach (var metric in metrics)
            {
                Assert.IsNull(metric.counter);
                Assert.IsNull(metric.histogram);
                Assert.IsNull(metric.summary);
                Assert.IsNull(metric.untyped);
                Assert.IsNotNull(metric.gauge);
            }

            Assert.AreEqual(3.8, metrics[0].gauge.value);
        }

        [TestMethod]
        public void histogram_tests()
        {
            Histogram histogram = Metrics.CreateHistogram("hist1", "help", new[] { 1.0, 2.0, 3.0, double.PositiveInfinity });
            histogram.Observe(1.5);
            histogram.Observe(2.5);
            histogram.Observe(1);
            histogram.Observe(2.4);
            histogram.Observe(2.1);
            histogram.Observe(0.4);
            histogram.Observe(1.4);
            histogram.Observe(1.5);
            histogram.Observe(3.9);
            histogram.Observe(double.NaN);

            var metric = histogram.Collect().Single().metric[0];
            Assert.IsNotNull(metric.histogram);
            Assert.AreEqual(9ul, metric.histogram.sample_count);
            Assert.AreEqual(16.7, metric.histogram.sample_sum);
            Assert.AreEqual(4, metric.histogram.bucket.Count);
            Assert.AreEqual(2ul, metric.histogram.bucket[0].cumulative_count);
            Assert.AreEqual(5ul, metric.histogram.bucket[1].cumulative_count);
            Assert.AreEqual(8ul, metric.histogram.bucket[2].cumulative_count);
            Assert.AreEqual(9ul, metric.histogram.bucket[3].cumulative_count);
        }

        [TestMethod]
        public void histogram_default_buckets()
        {
            var histogram = Metrics.CreateHistogram("hist", "help");
            histogram.Observe(0.03);

            var metric = histogram.Collect().Single().metric[0];
            Assert.IsNotNull(metric.histogram);
            Assert.AreEqual(1ul, metric.histogram.sample_count);
            Assert.AreEqual(0.03, metric.histogram.sample_sum);
            Assert.AreEqual(15, metric.histogram.bucket.Count);
            Assert.AreEqual(0.005, metric.histogram.bucket[0].upper_bound);
            Assert.AreEqual(0ul, metric.histogram.bucket[0].cumulative_count);
            Assert.AreEqual(0.01, metric.histogram.bucket[1].upper_bound);
            Assert.AreEqual(0ul, metric.histogram.bucket[1].cumulative_count);
            Assert.AreEqual(0.025, metric.histogram.bucket[2].upper_bound);
            Assert.AreEqual(0ul, metric.histogram.bucket[2].cumulative_count);
            Assert.AreEqual(0.05, metric.histogram.bucket[3].upper_bound);
            Assert.AreEqual(1ul, metric.histogram.bucket[3].cumulative_count);
            Assert.AreEqual(0.075, metric.histogram.bucket[4].upper_bound);
            Assert.AreEqual(1ul, metric.histogram.bucket[4].cumulative_count);
        }

        [TestMethod]
        public void histogram_no_buckets()
        {
            try
            {
                Metrics.CreateHistogram("hist", "help", new double[0]);
                Assert.Fail("Expected an exception");
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("Histogram must have at least one bucket", ex.Message);
            }
        }

        [TestMethod]
        public void histogram_buckets_do_not_increase()
        {
            try
            {
                Metrics.CreateHistogram("hist", "help", new double[] { 0.5, 0.1 });
                Assert.Fail("Expected an exception");
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("Bucket values must be increasing", ex.Message);
            }
        }
        
        [TestMethod]
        public void histogram_exponential_buckets_are_correct()
        {
            var bucketsStart = 1.1;
            var bucketsFactor = 2.4;
            var bucketsCount = 4;

            var buckets = Histogram.ExponentialBuckets(bucketsStart, bucketsFactor, bucketsCount);
            
            Assert.AreEqual(bucketsCount, buckets.Length);
            Assert.AreEqual(1.1, buckets[0]);
            Assert.AreEqual(2.64, buckets[1]);
            Assert.AreEqual(6.336, buckets[2]);
            Assert.AreEqual(15.2064, buckets[3]);
        }
        
        [TestMethod]
        public void histogram_exponential_buckets_with_non_positive_count_throws()
        {
            var bucketsStart = 1;
            var bucketsFactor = 2;

            Assert.ThrowsException<ArgumentException>(() => Histogram.ExponentialBuckets(bucketsStart, bucketsFactor, -1));
            Assert.ThrowsException<ArgumentException>(() => Histogram.ExponentialBuckets(bucketsStart, bucketsFactor, 0));
        }
        
        [TestMethod]
        public void histogram_exponential_buckets_with_non_positive_start_throws()
        {
            var bucketsFactor = 2;
            var bucketsCount = 5;

            Assert.ThrowsException<ArgumentException>(() => Histogram.ExponentialBuckets(-1, bucketsFactor, bucketsCount));
            Assert.ThrowsException<ArgumentException>(() => Histogram.ExponentialBuckets(0, bucketsFactor, bucketsCount));
        }
        
        [TestMethod]
        public void histogram_exponential_buckets_with__factor_less_than_one_throws()
        {
            var bucketsStart = 1;
            var bucketsCount = 5;

            Assert.ThrowsException<ArgumentException>(() => Histogram.ExponentialBuckets(bucketsStart, 0.9, bucketsCount));
            Assert.ThrowsException<ArgumentException>(() => Histogram.ExponentialBuckets(bucketsStart, 0, bucketsCount));
            Assert.ThrowsException<ArgumentException>(() => Histogram.ExponentialBuckets(bucketsStart, -1, bucketsCount));
        }
        
        [TestMethod]
        public void histogram_linear_buckets_are_correct()
        {
            var bucketsStart = 1.1;
            var bucketsWidth = 2.4;
            var bucketsCount = 4;

            var buckets = Histogram.LinearBuckets(bucketsStart, bucketsWidth, bucketsCount);
            
            Assert.AreEqual(bucketsCount, buckets.Length);
            Assert.AreEqual(1.1, buckets[0]);
            Assert.AreEqual(3.5, buckets[1]);
            Assert.AreEqual(5.9, buckets[2]);
            Assert.AreEqual(8.3, buckets[3]);
        }
        
        [TestMethod]
        public void histogram_linear_buckets_with_non_positive_count_throws()
        {
            var bucketsStart = 1;
            var bucketsWidth = 2;

            Assert.ThrowsException<ArgumentException>(() => Histogram.LinearBuckets(bucketsStart, bucketsWidth, -1));
            Assert.ThrowsException<ArgumentException>(() => Histogram.LinearBuckets(bucketsStart, bucketsWidth, 0));
        }

        [TestMethod]
        public void summary_tests()
        {
            var summary = Metrics.CreateSummary("summ1", "help");

            summary.Observe(1);
            summary.Observe(2);
            summary.Observe(3);

            var metric = summary.Collect().Single().metric[0];
            Assert.IsNotNull(metric.summary);
            Assert.AreEqual(3ul, metric.summary.sample_count);
            Assert.AreEqual(6, metric.summary.sample_sum);
        }

        [TestMethod]
        public void same_labels_return_same_instance()
        {
            var gauge = Metrics.CreateGauge("name1", "help1", "label1");

            var labelled1 = gauge.Labels("1");

            var labelled2 = gauge.Labels("1");

            Assert.AreSame(labelled2, labelled1);
        }

        [TestMethod]
        public void cannot_create_metrics_with_the_same_name_but_different_labels()
        {
            Metrics.CreateGauge("name1", "h");
            try
            {
                Metrics.CreateCounter("name1", "h", "label1");
                Assert.Fail("should have thrown");
            }
            catch (InvalidOperationException e)
            {
                Assert.AreEqual("Collector with same name must have same label names", e.Message);
            }
        }

        [TestMethod]
        public void metric_names()
        {
            Assert.ThrowsException<ArgumentException>(() => Metrics.CreateGauge("my-metric", "help"));
            Assert.ThrowsException<ArgumentException>(() => Metrics.CreateGauge("my!metric", "help"));
            Assert.ThrowsException<ArgumentException>(() => Metrics.CreateGauge("%", "help"));
            Assert.ThrowsException<ArgumentException>(() => Metrics.CreateGauge("5a", "help"));

            Metrics.CreateGauge("abc", "help");
            Metrics.CreateGauge("myMetric2", "help");
            Metrics.CreateGauge("a:3", "help");
        }

        [TestMethod]
        public void label_names()
        {
            Assert.ThrowsException<ArgumentException>(() => Metrics.CreateGauge("a", "help1", "my-metric"));
            Assert.ThrowsException<ArgumentException>(() => Metrics.CreateGauge("a", "help1", "my!metric"));
            Assert.ThrowsException<ArgumentException>(() => Metrics.CreateGauge("a", "help1", "my%metric"));
            Assert.ThrowsException<ArgumentException>(() => Metrics.CreateHistogram("a", "help1", null, "le"));
            Metrics.CreateGauge("a", "help1", "my:metric");
            Metrics.CreateGauge("b", "help1", "good_name");

            Assert.ThrowsException<ArgumentException>(() => Metrics.CreateGauge("c", "help1", "__reserved"));
        }

        [TestMethod]
        public void label_values()
        {
            var metric = Metrics.CreateGauge("a", "help1", "mylabelname");

            metric.Labels("");
            metric.Labels("mylabelvalue");
            Assert.ThrowsException<ArgumentNullException>(() => metric.Labels(null));
        }
    }
}
