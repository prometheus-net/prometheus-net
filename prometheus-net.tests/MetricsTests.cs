using System;
using System.Linq;
using NUnit.Framework;
using Prometheus.Advanced;
using Prometheus.Advanced.DataContracts;
using Shouldly;

namespace Prometheus.Tests
{
	[TestFixture]
	public class MetricsTests
    {
        [SetUp]
        public void Setup()
        {
            DefaultCollectorRegistry.Instance.Clear();
        }

        [Test]
        public void api_usage()
        {
            var gauge = Metrics.CreateGauge("name1", "help1");
            gauge.Inc();
            gauge.Value.ShouldBe(1);
            gauge.Inc(3.2);
            gauge.Value.ShouldBe(4.2);
            gauge.Set(4);
            gauge.Value.ShouldBe(4);
            gauge.Dec(0.2);
            gauge.Value.ShouldBe(3.8);

            Assert.Throws<InvalidOperationException>(() => gauge.Labels("1"));
            
            var counter = Metrics.CreateCounter("name2", "help2", "label1");
            counter.Inc();
            counter.Inc(3.2);
            counter.Inc(0);
            Assert.Throws<InvalidOperationException>(() => counter.Inc(-1));
            counter.Value.ShouldBe(4.2);

            counter.Labels("a").Value.ShouldBe(0);
            counter.Labels("a").Inc(3.3);
            counter.Labels("a").Inc(1.1);
            counter.Labels("a").Value.ShouldBe(4.4);
        }

        [Test]
        public void counter_collection()
        {
            var counter = Metrics.CreateCounter("name1", "help1", "label1");

            counter.Inc();
            counter.Inc(3.2);
            counter.Labels("abc").Inc(3.2);

            MetricFamily[] exported = DefaultCollectorRegistry.Instance.CollectAll().ToArray();

            exported.Length.ShouldBe(1);
            var familiy1 = exported[0];
            familiy1.name.ShouldBe("name1");
            familiy1.help.ShouldBe("help1");
            var metrics = familiy1.metric;
            metrics.Count.ShouldBe(2);

            foreach (var metric in metrics)
            {
                metric.gauge.ShouldBeNull();
                metric.histogram.ShouldBeNull();
                metric.summary.ShouldBeNull();
                metric.untyped.ShouldBeNull();
                metric.counter.ShouldNotBeNull();
            }

            metrics[0].counter.value.ShouldBe(4.2);
            metrics[0].label.Count.ShouldBe(0);
            
            metrics[1].counter.value.ShouldBe(3.2);
            var labelPairs = metrics[1].label;
            labelPairs.Count.ShouldBe(1);
            labelPairs[0].name.ShouldBe("label1");
            labelPairs[0].value.ShouldBe("abc");
        }

        [Test]
        public void custom_registry()
        {
            var myRegistry = new DefaultCollectorRegistry();
            var counter1 = Metrics.WithCustomRegistry(myRegistry).CreateCounter("counter1", "help1"); //registered on a custom registry
            
            var counter2 = Metrics.CreateCounter("counter1", "help1"); //created on different registry - same name is hence permitted

            counter1.Inc(3);
            counter2.Inc(4);

            myRegistry.CollectAll().ToArray()[0].metric[0].counter.value.ShouldBe(3); //counter1 == 3
            DefaultCollectorRegistry.Instance.CollectAll().ToArray()[0].metric[0].counter.value.ShouldBe(4); //counter2 == 4
        }

        [Test]
        public void gauge_collection()
        {
            var gauge = Metrics.CreateGauge("name1", "help1");

            gauge.Inc();
            gauge.Inc(3.2);
            gauge.Set(4);
            gauge.Dec(0.2);

            var exported = DefaultCollectorRegistry.Instance.CollectAll().ToArray();

            exported.Length.ShouldBe(1);
            var familiy1 = exported[0];
            familiy1.name.ShouldBe("name1");
            familiy1.help.ShouldBe("help1");
            var metrics = familiy1.metric;
            metrics.Count.ShouldBe(1);

            foreach (var metric in metrics)
            {
                metric.counter.ShouldBeNull();
                metric.histogram.ShouldBeNull();
                metric.summary.ShouldBeNull();
                metric.untyped.ShouldBeNull();
                metric.gauge.ShouldNotBeNull();
            }

            metrics[0].gauge.value.ShouldBe(3.8);
        }

        [Test]
        public void histogram_tests()
        {
            Histogram histogram = Metrics.CreateHistogram("hist1", "help", new []{ 1.0, 2.0, 3.0, double.PositiveInfinity});
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

            var metric = histogram.Collect().metric[0];
            metric.histogram.ShouldNotBeNull();
            metric.histogram.sample_count.ShouldBe(9ul);
            metric.histogram.sample_sum.ShouldBe(16.7);
            metric.histogram.bucket.Count.ShouldBe(4);
            metric.histogram.bucket[0].cumulative_count.ShouldBe(2ul);
            metric.histogram.bucket[1].cumulative_count.ShouldBe(5ul);
            metric.histogram.bucket[2].cumulative_count.ShouldBe(8ul);
            metric.histogram.bucket[3].cumulative_count.ShouldBe(9ul);
        }

        [Test]
        public void histogram_default_buckets()
        {
            var histogram = Metrics.CreateHistogram("hist", "help");
            histogram.Observe(0.03);

            var metric = histogram.Collect().metric[0];
            metric.histogram.ShouldNotBeNull();
            metric.histogram.sample_count.ShouldBe(1ul);
            metric.histogram.sample_sum.ShouldBe(0.03);
            metric.histogram.bucket.Count.ShouldBe(15);
            metric.histogram.bucket[0].upper_bound.ShouldBe(0.005);
            metric.histogram.bucket[0].cumulative_count.ShouldBe(0ul);
            metric.histogram.bucket[1].upper_bound.ShouldBe(0.01);
            metric.histogram.bucket[1].cumulative_count.ShouldBe(0ul);
            metric.histogram.bucket[2].upper_bound.ShouldBe(0.025);
            metric.histogram.bucket[2].cumulative_count.ShouldBe(0ul);
            metric.histogram.bucket[3].upper_bound.ShouldBe(0.05);
            metric.histogram.bucket[3].cumulative_count.ShouldBe(1ul);
            metric.histogram.bucket[4].upper_bound.ShouldBe(0.075);
            metric.histogram.bucket[4].cumulative_count.ShouldBe(1ul);
        }

        [Test]
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

        [Test]
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

        [Test]
        public void summary_tests()
        {
            var summary = Metrics.CreateSummary("summ1", "help");

            summary.Observe(1);
            summary.Observe(2);
            summary.Observe(3);

            var metric = summary.Collect().metric[0];
            metric.summary.ShouldNotBeNull();
            metric.summary.sample_count.ShouldBe(3ul);
            metric.summary.sample_sum.ShouldBe(6);
        }

        [Test]
        public void same_labels_return_same_instance()
        {
            var gauge = Metrics.CreateGauge("name1", "help1", "label1");
            
            var labelled1 = gauge.Labels("1");

            var labelled2 = gauge.Labels("1");

            labelled1.ShouldBeSameAs(labelled2);
        }

        [Test]
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
                e.Message.ShouldBe("Collector with same name must have same label names");
            }
        }

        [Test]
        public void metric_names()
        {
            Assert.Throws<ArgumentException>(() => Metrics.CreateGauge("my-metric", "help"));
            Assert.Throws<ArgumentException>(() => Metrics.CreateGauge("my!metric", "help"));
            Assert.Throws<ArgumentException>(() => Metrics.CreateGauge("%", "help"));
            Assert.Throws<ArgumentException>(() => Metrics.CreateGauge("5a", "help"));

            Metrics.CreateGauge("abc", "help");
            Metrics.CreateGauge("myMetric2", "help");
            Metrics.CreateGauge("a:3", "help");
        }

        [Test]
        public void label_names()
        {
            Assert.Throws<ArgumentException>(() => Metrics.CreateGauge("a", "help1", "my-metric"));
            Assert.Throws<ArgumentException>(() => Metrics.CreateGauge("a", "help1", "my!metric"));
            Assert.Throws<ArgumentException>(() => Metrics.CreateGauge("a", "help1", "my%metric"));
            Assert.Throws<ArgumentException>(() => Metrics.CreateHistogram("a", "help1", null, "le"));
            Metrics.CreateGauge("a", "help1", "my:metric");
            Metrics.CreateGauge("b", "help1", "good_name");

            try
            {
                Metrics.CreateGauge("c", "help1", "__reserved");
            }
            catch (ArgumentException e)
            {
                e.Message.ShouldBe("Labels starting with double underscore are reserved!");
            }
        }
    }
}
