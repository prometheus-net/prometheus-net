using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Prometheus.Tests
{
    [TestClass]
    public class MetricsTests
    {
        public MetricsTests()
        {
            Metrics.SuppressDefaultMetrics();
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

            var exported = Metrics.DefaultRegistry.Collect().Families;

            Assert.AreEqual(1, exported.Count);
            var familiy1 = exported[0];
            Assert.AreEqual("name1", familiy1.Name);
            Assert.AreEqual("help1", familiy1.Help);
            var metrics = familiy1.Metrics;
            Assert.AreEqual(2, metrics.Count);

            // We need to sort the metrics as the order they are returned in is not fixed.
            // Let's just sort by counter value, descending (arbitrarily).
            metrics.Sort((a, b) => -a.Counter.Value.CompareTo(b.Counter.Value));

            foreach (var metric in metrics)
            {
                Assert.IsNull(metric.Gauge);
                Assert.IsNull(metric.Histogram);
                Assert.IsNull(metric.Summary);
                Assert.IsNotNull(metric.Counter);
            }

            Assert.AreEqual(4.2, metrics[0].Counter.Value);
            Assert.AreEqual(0, metrics[0].Labels.Length);

            Assert.AreEqual(3.2, metrics[1].Counter.Value);
            var labelPairs = metrics[1].Labels;
            Assert.AreEqual(1, labelPairs.Length);
            Assert.AreEqual("label1", labelPairs[0].Name);
            Assert.AreEqual("abc", labelPairs[0].Value);
        }

        [TestMethod]
        public void custom_registry()
        {
            var myRegistry = Metrics.NewCustomRegistry();
            var counter1 = Metrics.WithCustomRegistry(myRegistry).CreateCounter("counter1", "help1"); //registered on a custom registry

            var counter2 = Metrics.CreateCounter("counter1", "help1"); //created on different registry - same name is hence permitted

            counter1.Inc(3);
            counter2.Inc(4);

            Assert.AreEqual(3, myRegistry.Collect().Families[0].Metrics[0].Counter.Value); //counter1 == 3
            Assert.AreEqual(4, Metrics.DefaultRegistry.Collect().Families[0].Metrics[0].Counter.Value); //counter2 == 4
        }

        [TestMethod]
        public void gauge_collection()
        {
            var gauge = Metrics.CreateGauge("name1", "help1");

            gauge.Inc();
            gauge.Inc(3.2);
            gauge.Set(4);
            gauge.Dec(0.2);

            var exported = Metrics.DefaultRegistry.Collect().Families;

            Assert.AreEqual(1, exported.Count);
            var familiy1 = exported[0];
            Assert.AreEqual("name1", familiy1.Name);
            Assert.AreEqual("help1", familiy1.Help);
            var metrics = familiy1.Metrics;
            Assert.AreEqual(1, metrics.Count);

            foreach (var metric in metrics)
            {
                Assert.IsNull(metric.Counter);
                Assert.IsNull(metric.Histogram);
                Assert.IsNull(metric.Summary);
                Assert.IsNotNull(metric.Gauge);
            }

            Assert.AreEqual(3.8, metrics[0].Gauge.Value);
        }

        [TestMethod]
        public void histogram_tests()
        {
            Histogram histogram = Metrics.CreateHistogram("hist1", "help", new HistogramConfiguration
            {
                Buckets = new[] { 1.0, 2.0, 3.0, double.PositiveInfinity }
            });

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

            var metric = histogram.Collect().Metrics[0];
            Assert.IsNotNull(metric.Histogram);
            Assert.AreEqual(9L, metric.Histogram.SampleCount);
            Assert.AreEqual(16.7, metric.Histogram.SampleSum);
            Assert.AreEqual(4, metric.Histogram.Buckets.Length);
            Assert.AreEqual(2L, metric.Histogram.Buckets[0].CumulativeCount);
            Assert.AreEqual(5L, metric.Histogram.Buckets[1].CumulativeCount);
            Assert.AreEqual(8L, metric.Histogram.Buckets[2].CumulativeCount);
            Assert.AreEqual(9L, metric.Histogram.Buckets[3].CumulativeCount);
        }

        [TestMethod]
        public void histogram_default_buckets()
        {
            var histogram = Metrics.CreateHistogram("hist", "help");
            histogram.Observe(0.03);

            var metric = histogram.Collect().Metrics[0];
            Assert.IsNotNull(metric.Histogram);
            Assert.AreEqual(1L, metric.Histogram.SampleCount);
            Assert.AreEqual(0.03, metric.Histogram.SampleSum);
            Assert.AreEqual(15, metric.Histogram.Buckets.Length);
            Assert.AreEqual(0.005, metric.Histogram.Buckets[0].UpperBound);
            Assert.AreEqual(0L, metric.Histogram.Buckets[0].CumulativeCount);
            Assert.AreEqual(0.01, metric.Histogram.Buckets[1].UpperBound);
            Assert.AreEqual(0L, metric.Histogram.Buckets[1].CumulativeCount);
            Assert.AreEqual(0.025, metric.Histogram.Buckets[2].UpperBound);
            Assert.AreEqual(0L, metric.Histogram.Buckets[2].CumulativeCount);
            Assert.AreEqual(0.05, metric.Histogram.Buckets[3].UpperBound);
            Assert.AreEqual(1L, metric.Histogram.Buckets[3].CumulativeCount);
            Assert.AreEqual(0.075, metric.Histogram.Buckets[4].UpperBound);
            Assert.AreEqual(1L, metric.Histogram.Buckets[4].CumulativeCount);
        }

        [TestMethod]
        public void histogram_no_buckets()
        {
            try
            {
                Metrics.CreateHistogram("hist", "help", new HistogramConfiguration
                {
                    Buckets = new double[0]
                });

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
                Metrics.CreateHistogram("hist", "help", new HistogramConfiguration
                {
                    Buckets = new double[] { 0.5, 0.1 }
                });

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

            var metric = summary.Collect().Metrics[0];
            Assert.IsNotNull(metric.Summary);
            Assert.AreEqual(3L, metric.Summary.SampleCount);
            Assert.AreEqual(6, metric.Summary.SampleSum);
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
            Assert.ThrowsException<ArgumentException>(() => Metrics.CreateHistogram("a", "help1", "le"));
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
