using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus.Advanced;
using System.Linq;

namespace Prometheus.Tests
{
    [TestClass]
    public sealed class MetricInitializationTests
    {
        #region Unlabelled logic
        [TestMethod]
        public void CreatingUnlabelledMetric_WithoutObservingAnyData_ExportsImmediately()
        {
            var registry = new DefaultCollectorRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var gauge = factory.CreateGauge("gauge", "", new GaugeConfiguration
            {
            });
            var counter = factory.CreateCounter("counter", "", new CounterConfiguration
            {
            });
            var summary = factory.CreateSummary("summary", "", new SummaryConfiguration
            {
            });
            var histogram = factory.CreateHistogram("histogram", "", new HistogramConfiguration
            {
            });

            // Without touching any metrics, there should be output for all because default config publishes immediately.
            var exported = registry.CollectAll().ToArray();

            // There is a family for each of the above, in each family we expect to see 1 metrics.
            Assert.AreEqual(4, exported.Length);

            foreach (var family in exported)
                Assert.AreEqual(1, family.metric.Count, $"Family {family.type} had unexpected metric count.");
        }

        [TestMethod]
        public void CreatingUnlabelledMetric_WithInitialValueSuppression_ExportsNothingByDefault()
        {
            var registry = new DefaultCollectorRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var gauge = factory.CreateGauge("gauge", "", new GaugeConfiguration
            {
                SuppressInitialValue = true
            });
            var counter = factory.CreateCounter("counter", "", new CounterConfiguration
            {
                SuppressInitialValue = true
            });
            var summary = factory.CreateSummary("summary", "", new SummaryConfiguration
            {
                SuppressInitialValue = true
            });
            var histogram = factory.CreateHistogram("histogram", "", new HistogramConfiguration
            {
                SuppressInitialValue = true
            });

            var exported = registry.CollectAll().ToArray();

            // There is a family for each of the above, in each family we expect to see 0 metrics.
            Assert.AreEqual(4, exported.Length);

            foreach (var family in exported)
                Assert.AreEqual(0, family.metric.Count, $"Family {family.type} had unexpected metric count.");
        }

        [TestMethod]
        public void CreatingUnlabelledMetric_WithInitialValueSuppression_ExportsAfterValueChange()
        {
            var registry = new DefaultCollectorRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var gauge = factory.CreateGauge("gauge", "", new GaugeConfiguration
            {
                SuppressInitialValue = true
            });
            var counter = factory.CreateCounter("counter", "", new CounterConfiguration
            {
                SuppressInitialValue = true
            });
            var summary = factory.CreateSummary("summary", "", new SummaryConfiguration
            {
                SuppressInitialValue = true
            });
            var histogram = factory.CreateHistogram("histogram", "", new HistogramConfiguration
            {
                SuppressInitialValue = true
            });

            gauge.Set(123);
            counter.Inc();
            summary.Observe(123);
            histogram.Observe(31);

            // Without touching any metrics, there should be output for all because default config publishes immediately.
            var exported = registry.CollectAll().ToArray();

            // There is a family for each of the above, in each family we expect to see 1 metric.
            Assert.AreEqual(4, exported.Length);

            foreach (var family in exported)
                Assert.AreEqual(1, family.metric.Count, $"Family {family.type} had unexpected metric count.");
        }

        [TestMethod]
        public void CreatingUnlabelledMetric_WithInitialValueSuppression_ExportsAfterPublish()
        {
            var registry = new DefaultCollectorRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var gauge = factory.CreateGauge("gauge", "", new GaugeConfiguration
            {
                SuppressInitialValue = true
            });
            var counter = factory.CreateCounter("counter", "", new CounterConfiguration
            {
                SuppressInitialValue = true
            });
            var summary = factory.CreateSummary("summary", "", new SummaryConfiguration
            {
                SuppressInitialValue = true
            });
            var histogram = factory.CreateHistogram("histogram", "", new HistogramConfiguration
            {
                SuppressInitialValue = true
            });

            gauge.Publish();
            counter.Publish();
            summary.Publish();
            histogram.Publish();

            // Without touching any metrics, there should be output for all because default config publishes immediately.
            var exported = registry.CollectAll().ToArray();

            // There is a family for each of the above, in each family we expect to see 1 metrics.
            Assert.AreEqual(4, exported.Length);

            foreach (var family in exported)
                Assert.AreEqual(1, family.metric.Count, $"Family {family.type} had unexpected metric count.");
        }
        #endregion

        #region Labelled logic
        [TestMethod]
        public void CreatingLabelledMetric_WithoutObservingAnyData_ExportsImmediately()
        {
            var registry = new DefaultCollectorRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var gauge = factory.CreateGauge("gauge", "", new GaugeConfiguration
            {
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");
            var counter = factory.CreateCounter("counter", "", new CounterConfiguration
            {
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");
            var summary = factory.CreateSummary("summary", "", new SummaryConfiguration
            {
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");
            var histogram = factory.CreateHistogram("histogram", "", new HistogramConfiguration
            {
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");

            // Without touching any metrics, there should be output for all because default config publishes immediately.
            var exported = registry.CollectAll().ToArray();

            // There is a family for each of the above, in each family we expect to see 1 metrics.
            Assert.AreEqual(4, exported.Length);

            foreach (var family in exported)
                Assert.AreEqual(1, family.metric.Count, $"Family {family.type} had unexpected metric count.");
        }

        [TestMethod]
        public void CreatingLabelledMetric_WithInitialValueSuppression_ExportsNothingByDefault()
        {
            var registry = new DefaultCollectorRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var gauge = factory.CreateGauge("gauge", "", new GaugeConfiguration
            {
                SuppressInitialValue = true,
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");
            var counter = factory.CreateCounter("counter", "", new CounterConfiguration
            {
                SuppressInitialValue = true,
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");
            var summary = factory.CreateSummary("summary", "", new SummaryConfiguration
            {
                SuppressInitialValue = true,
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");
            var histogram = factory.CreateHistogram("histogram", "", new HistogramConfiguration
            {
                SuppressInitialValue = true,
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");

            var exported = registry.CollectAll().ToArray();

            // There is a family for each of the above, in each family we expect to see 0 metrics.
            Assert.AreEqual(4, exported.Length);

            foreach (var family in exported)
                Assert.AreEqual(0, family.metric.Count, $"Family {family.type} had unexpected metric count.");
        }

        [TestMethod]
        public void CreatingLabelledMetric_WithInitialValueSuppression_ExportsAfterValueChange()
        {
            var registry = new DefaultCollectorRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var gauge = factory.CreateGauge("gauge", "", new GaugeConfiguration
            {
                SuppressInitialValue = true,
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");
            var counter = factory.CreateCounter("counter", "", new CounterConfiguration
            {
                SuppressInitialValue = true,
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");
            var summary = factory.CreateSummary("summary", "", new SummaryConfiguration
            {
                SuppressInitialValue = true,
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");
            var histogram = factory.CreateHistogram("histogram", "", new HistogramConfiguration
            {
                SuppressInitialValue = true,
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");

            gauge.Set(123);
            counter.Inc();
            summary.Observe(123);
            histogram.Observe(31);

            // Without touching any metrics, there should be output for all because default config publishes immediately.
            var exported = registry.CollectAll().ToArray();

            // There is a family for each of the above, in each family we expect to see 1 metric.
            Assert.AreEqual(4, exported.Length);

            foreach (var family in exported)
                Assert.AreEqual(1, family.metric.Count, $"Family {family.type} had unexpected metric count.");
        }

        [TestMethod]
        public void CreatingLabelledMetric_WithInitialValueSuppression_ExportsAfterPublish()
        {
            var registry = new DefaultCollectorRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var gauge = factory.CreateGauge("gauge", "", new GaugeConfiguration
            {
                SuppressInitialValue = true,
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");
            var counter = factory.CreateCounter("counter", "", new CounterConfiguration
            {
                SuppressInitialValue = true,
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");
            var summary = factory.CreateSummary("summary", "", new SummaryConfiguration
            {
                SuppressInitialValue = true,
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");
            var histogram = factory.CreateHistogram("histogram", "", new HistogramConfiguration
            {
                SuppressInitialValue = true,
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");

            gauge.Publish();
            counter.Publish();
            summary.Publish();
            histogram.Publish();

            // Without touching any metrics, there should be output for all because default config publishes immediately.
            var exported = registry.CollectAll().ToArray();

            // There is a family for each of the above, in each family we expect to see 1 metrics.
            Assert.AreEqual(4, exported.Length);

            foreach (var family in exported)
                Assert.AreEqual(1, family.metric.Count, $"Family {family.type} had unexpected metric count.");
        }
        #endregion

        #region Relation between labelled and unlabelled
        [TestMethod]
        public void CreatingLabelledMetric_WithoutObservingAnyData_DoesNotExportUnlabelled()
        {
            var registry = new DefaultCollectorRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var gauge = factory.CreateGauge("gauge", "", "labelname");
            var counter = factory.CreateCounter("counter", "", "labelname");
            var summary = factory.CreateSummary("summary", "", "labelname");
            var histogram = factory.CreateHistogram("histogram", "", null, "labelname");

            // Without touching any metrics, there should be no output.
            var exported = registry.CollectAll().ToArray();

            // There is a family for each of the above, in each family we expect to see 0 metrics.
            Assert.AreEqual(4, exported.Length);

            foreach (var family in exported)
                Assert.AreEqual(0, family.metric.Count, $"Family {family.type} had unexpected metric count.");
        }

        [TestMethod]
        public void CreatingLabelledMetric_AfterObservingLabelledData_DoesNotExportUnlabelled()
        {
            var registry = new DefaultCollectorRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var gauge = factory.CreateGauge("gauge", "", "labelname");
            var counter = factory.CreateCounter("counter", "", "labelname");
            var summary = factory.CreateSummary("summary", "", "labelname");
            var histogram = factory.CreateHistogram("histogram", "", null, "labelname");

            // Touch some labelled metrics.
            gauge.Labels("labelvalue").Inc();
            counter.Labels("labelvalue").Inc();
            summary.Labels("labelvalue").Observe(123);
            histogram.Labels("labelvalue").Observe(123);

            // Without touching any unlabelled metrics, there should be only labelled output.
            var exported = registry.CollectAll().ToArray();

            // There is a family for each of the above, in each family we expect to see 1 metric (for the labelled case).
            Assert.AreEqual(4, exported.Length);

            foreach (var family in exported)
                Assert.AreEqual(1, family.metric.Count, $"Family {family.type} had unexpected metric count.");
        }
        #endregion
    }
}
