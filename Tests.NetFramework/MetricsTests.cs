using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Prometheus.Tests
{
    [TestClass]
    public class MetricsTests
    {
        private CollectorRegistry _registry;
        private MetricFactory _metrics;

        public MetricsTests()
        {
            _registry = Metrics.NewCustomRegistry();
            _metrics = Metrics.WithCustomRegistry(_registry);
        }

        [TestMethod]
        public void api_usage()
        {
            var gauge = _metrics.CreateGauge("name1", "help1");
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
        public async Task CreateCounter_WithDifferentRegistry_CreatesIndependentCounters()
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
            await registry1.CollectAndSerializeAsync(serializer1, default);

            var serializer2 = Substitute.For<IMetricsSerializer>();
            await registry2.CollectAndSerializeAsync(serializer2, default);

            await serializer1.ReceivedWithAnyArgs().WriteFamilyDeclarationAsync(default, default);
            await serializer1.ReceivedWithAnyArgs().WriteMetricAsync(default, default, default);

            await serializer2.ReceivedWithAnyArgs().WriteFamilyDeclarationAsync(default, default);
            await serializer2.ReceivedWithAnyArgs().WriteMetricAsync(default, default, default);
        }

        [TestMethod]
        public async Task Export_FamilyWithOnlyNonpublishedUnlabeledMetrics_ExportsFamilyDeclaration()
        {
            // See https://github.com/prometheus-net/prometheus-net/issues/196
            var metric = _metrics.CreateCounter("my_family", "", new CounterConfiguration
            {
                SuppressInitialValue = true
            });

            var serializer = Substitute.For<IMetricsSerializer>();
            await _registry.CollectAndSerializeAsync(serializer, default);

            await serializer.ReceivedWithAnyArgs(1).WriteFamilyDeclarationAsync(default, default);
            await serializer.DidNotReceiveWithAnyArgs().WriteMetricAsync(default, default, default);
            serializer.ClearReceivedCalls();

            metric.Inc();
            metric.Unpublish();

            await _registry.CollectAndSerializeAsync(serializer, default);

            await serializer.ReceivedWithAnyArgs(1).WriteFamilyDeclarationAsync(default, default);
            await serializer.DidNotReceiveWithAnyArgs().WriteMetricAsync(default, default, default);
        }

        [TestMethod]
        public async Task Export_FamilyWithOnlyNonpublishedLabeledMetrics_ExportsFamilyDeclaration()
        {
            // See https://github.com/prometheus-net/prometheus-net/issues/196
            var metric = _metrics.CreateCounter("my_family", "", new CounterConfiguration
            {
                SuppressInitialValue = true,
                LabelNames = new[] { "labelname" }
            });

            var instance = metric.WithLabels("labelvalue");

            var serializer = Substitute.For<IMetricsSerializer>();
            await _registry.CollectAndSerializeAsync(serializer, default);

            await serializer.ReceivedWithAnyArgs(1).WriteFamilyDeclarationAsync(default, default);
            await serializer.DidNotReceiveWithAnyArgs().WriteMetricAsync(default, default, default);
            serializer.ClearReceivedCalls();

            instance.Inc();
            instance.Unpublish();

            await _registry.CollectAndSerializeAsync(serializer, default);

            await serializer.ReceivedWithAnyArgs(1).WriteFamilyDeclarationAsync(default, default);
            await serializer.DidNotReceiveWithAnyArgs().WriteMetricAsync(default, default, default);
        }

        [TestMethod]
        public async Task DisposeChild_RemovesMetric()
        {
            var metric = _metrics.CreateCounter("my_family", "", new CounterConfiguration
            {
                SuppressInitialValue = true,
                LabelNames = new[] { "labelname" }
            });

            var instance = metric.WithLabels("labelvalue");
            instance.Inc();

            var serializer = Substitute.For<IMetricsSerializer>();
            await _registry.CollectAndSerializeAsync(serializer, default);

            await serializer.ReceivedWithAnyArgs(1).WriteFamilyDeclarationAsync(default, default);
            await serializer.ReceivedWithAnyArgs(1).WriteMetricAsync(default, default, default);
            serializer.ClearReceivedCalls();

            instance.Dispose();

            await _registry.CollectAndSerializeAsync(serializer, default);

            await serializer.ReceivedWithAnyArgs(1).WriteFamilyDeclarationAsync(default, default);
            await serializer.DidNotReceiveWithAnyArgs().WriteMetricAsync(default, default, default);
        }

        [TestMethod]
        public void histogram_no_buckets()
        {
            try
            {
                _metrics.CreateHistogram("hist", "help", new HistogramConfiguration
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
                _metrics.CreateHistogram("hist", "help", new HistogramConfiguration
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
            var gauge = _metrics.CreateGauge("name1", "help1", "label1");

            var labelled1 = gauge.Labels("1");

            var labelled2 = gauge.Labels("1");

            Assert.AreSame(labelled2, labelled1);
        }

        [TestMethod]
        public void cannot_create_metrics_with_the_same_name_but_different_labels()
        {
            _metrics.CreateGauge("name1", "h");
            try
            {
                _metrics.CreateGauge("name1", "h", "label1");
                Assert.Fail("should have thrown");
            }
            catch (InvalidOperationException e)
            {
                Assert.AreEqual("Collector matches a previous registration but has a different set of label names.", e.Message);
            }
        }

        [TestMethod]
        public void cannot_create_metrics_with_the_same_name_and_labels_but_different_type()
        {
            _metrics.CreateGauge("name1", "h", "label1");
            try
            {
                _metrics.CreateCounter("name1", "h", "label1");
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
            Assert.ThrowsException<ArgumentException>(() => _metrics.CreateGauge("my-metric", "help"));
            Assert.ThrowsException<ArgumentException>(() => _metrics.CreateGauge("my!metric", "help"));
            Assert.ThrowsException<ArgumentException>(() => _metrics.CreateGauge("%", "help"));
            Assert.ThrowsException<ArgumentException>(() => _metrics.CreateGauge("5a", "help"));

            _metrics.CreateGauge("abc", "help");
            _metrics.CreateGauge("myMetric2", "help");
            _metrics.CreateGauge("a:3", "help");
        }

        [TestMethod]
        public void label_names()
        {
            Assert.ThrowsException<ArgumentException>(() => _metrics.CreateGauge("a", "help1", "my-metric"));
            Assert.ThrowsException<ArgumentException>(() => _metrics.CreateGauge("a", "help1", "my!metric"));
            Assert.ThrowsException<ArgumentException>(() => _metrics.CreateGauge("a", "help1", "my%metric"));
            Assert.ThrowsException<ArgumentException>(() => _metrics.CreateHistogram("a", "help1", "le"));
            _metrics.CreateGauge("a", "help1", "my:metric");
            _metrics.CreateGauge("b", "help1", "good_name");

            Assert.ThrowsException<ArgumentException>(() => _metrics.CreateGauge("c", "help1", "__reserved"));
        }

        [TestMethod]
        public void label_values()
        {
            var metric = _metrics.CreateGauge("a", "help1", "mylabelname");

            metric.Labels("");
            metric.Labels("mylabelvalue");
            Assert.ThrowsException<ArgumentNullException>(() => metric.Labels(null));
        }

        [TestMethod]
        public void GetAllLabelValues_GetsThemAll()
        {
            var metric = _metrics.CreateGauge("ahdgfln", "ahegrtijpm", "a", "b", "c");
            metric.Labels("1", "2", "3");
            metric.Labels("4", "5", "6");

            var values = metric.GetAllLabelValues().OrderBy(v => v[0]).ToArray();

            Assert.AreEqual(2, values.Length);

            Assert.AreEqual(3, values[0].Length);
            Assert.AreEqual("1", values[0][0]);
            Assert.AreEqual("2", values[0][1]);
            Assert.AreEqual("3", values[0][2]);

            Assert.AreEqual(3, values[1].Length);
            Assert.AreEqual("4", values[1][0]);
            Assert.AreEqual("5", values[1][1]);
            Assert.AreEqual("6", values[1][2]);
        }

        [TestMethod]
        public void GetAllLabelValues_DoesNotGetUnlabelled()
        {
            var metric = _metrics.CreateGauge("ahdggfagfln", "ahegrgftijpm");
            metric.Inc();

            var values = metric.GetAllLabelValues().ToArray();

            Assert.AreEqual(0, values.Length);
        }
    }
}
