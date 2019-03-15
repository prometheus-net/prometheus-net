using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System;

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
        public void CreateCounter_WithDifferentRegistry_CreatesIndependentCounters()
        {
            var registry1 = Metrics.NewCustomRegistry();
            var registry2 = Metrics.NewCustomRegistry();
            var counter1 = Metrics.WithCustomRegistry(registry1)
                .CreateCounter("counter", "");
            var counter2 = Metrics.WithCustomRegistry(registry2)
                .CreateCounter("counter", "");

            Assert.AreNotSame(counter1, counter2);

            counter1.Inc();
            counter2.Inc();

            Assert.AreEqual(1, counter1.Value);
            Assert.AreEqual(1, counter2.Value);

            var serializer1 = Substitute.For<IMetricsSerializer>();
            registry1.CollectAndSerialize(serializer1);

            var serializer2 = Substitute.For<IMetricsSerializer>();
            registry2.CollectAndSerialize(serializer2);

            serializer1.ReceivedWithAnyArgs().WriteFamilyDeclaration(default);
            serializer1.ReceivedWithAnyArgs().WriteMetric(default, default);

            serializer2.ReceivedWithAnyArgs().WriteFamilyDeclaration(default);
            serializer2.ReceivedWithAnyArgs().WriteMetric(default, default);
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
                Metrics.CreateGauge("name1", "h", "label1");
                Assert.Fail("should have thrown");
            }
            catch (InvalidOperationException e)
            {
                Assert.AreEqual("Collector with same name must have same label names", e.Message);
            }
        }

        [TestMethod]
        public void cannot_create_metrics_with_the_same_name_and_labels_but_different_type()
        {
            Metrics.CreateGauge("name1", "h", "label1");
            try
            {
                Metrics.CreateCounter("name1", "h", "label1");
                Assert.Fail("should have thrown");
            }
            catch (InvalidOperationException e)
            {
                Assert.AreEqual("Collector of a different type with the same name is already registered.", e.Message);
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
