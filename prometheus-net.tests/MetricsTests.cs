using System;
using System.Diagnostics;
using System.Linq;
using io.prometheus.client;
using NUnit.Framework;
using Prometheus.Internal;
using Should;

namespace Prometheus.Tests
{
    public class MetricsTests
    {
        [SetUp]
        public void setup()
        {
            MetricsRegistry.Instance.Clear();
        }

        [Test]
        public void api_usage()
        {
            var gauge = Metrics.CreateGauge("name1", "help1");
            gauge.Inc();
            gauge.Value.ShouldEqual(1);
            gauge.Inc(3.2);
            gauge.Value.ShouldEqual(4.2);
            gauge.Observe(4);
            gauge.Value.ShouldEqual(4);
            gauge.Dec(0.2);
            gauge.Value.ShouldEqual(3.8);

            Assert.Throws<InvalidOperationException>(() => gauge.Labels("1"));
            

            var counter = Metrics.CreateCounter("name2", "help2", "label1");
            counter.Inc();
            counter.Inc(3.2);
            counter.Value.ShouldEqual(4.2);

            counter.Labels("a").Value.ShouldEqual(0);
            counter.Labels("a").Inc(3.3);
            counter.Labels("a").Inc(1.1);
            counter.Labels("a").Value.ShouldEqual(4.4);
        }

        [Test]
        public void api_usage_labels()
        {
            var gauge = Metrics.CreateGauge("name1", "help1");
            gauge.Inc();
            gauge.Value.ShouldEqual(1);

        }

        [Test]
        public void gauge_collection()
        {
            var gauge = Metrics.CreateGauge("name1", "help1");

            gauge.Inc();
            gauge.Inc(3.2);
            gauge.Observe(4);
            gauge.Dec(0.2);

            var exported = MetricsRegistry.Instance.CollectAll().ToArray();

            exported.Length.ShouldEqual(1);
            var familiy1 = exported[0];
            familiy1.name.ShouldEqual("name1");
            familiy1.help.ShouldEqual("help1");
            var metrics = familiy1.metric;
            metrics.Count.ShouldEqual(1);

            foreach (var metric in metrics)
            {
                metric.counter.ShouldBeNull();
                metric.histogram.ShouldBeNull();
                metric.summary.ShouldBeNull();
                metric.untyped.ShouldBeNull();
                metric.gauge.ShouldNotBeNull();
            }

            metrics[0].gauge.value.ShouldEqual(3.8);
        }

      

        [Test]
        public void histogram_tests()
        {
            Histogram histogram = Metrics.CreateHistogram("hist1", "help", new []{ 1.0, 2.0, 3.0});
            histogram.Observe(1.5);
            histogram.Observe(2.5);
            histogram.Observe(1);
            histogram.Observe(2.4);
            histogram.Observe(2.1);
            histogram.Observe(0.4);
            histogram.Observe(1.4);
            histogram.Observe(1.5);
            histogram.Observe(3.9);

            var metric = histogram.Collect();
            metric.histogram.ShouldNotBeNull();
            metric.histogram.sample_count.ShouldEqual(9ul);
            metric.histogram.sample_sum.ShouldEqual(16.7);
            metric.histogram.bucket.Count.ShouldEqual(4);
            metric.histogram.bucket[0].cumulative_count.ShouldEqual(2ul);
            metric.histogram.bucket[1].cumulative_count.ShouldEqual(5ul);
            metric.histogram.bucket[2].cumulative_count.ShouldEqual(8ul);
            metric.histogram.bucket[3].cumulative_count.ShouldEqual(9ul);
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
        public void cannot_create_different_types_of_metrics_with_the_same_name()
        {
            Metrics.CreateGauge("name1", "h");
            try
            {
                Metrics.CreateCounter("name1", "h");
                Assert.Fail("should have thrown");
            }
            catch (InvalidOperationException e)
            {
                e.Message.ShouldEqual("A metric of type Gauge has already been declared with name 'name1'");
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
            Metrics.CreateGauge("a", "help1", "my:metric");
            Metrics.CreateGauge("a", "help1", "good_name");

            try
            {
                Metrics.CreateGauge("a", "help1", "__reserved");
            }
            catch (ArgumentException e)
            {
                e.Message.ShouldEqual("LabelValues starting with double underscore are reserved!");
            }
        }
    }
}
