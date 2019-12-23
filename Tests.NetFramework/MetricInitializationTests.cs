using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Threading.Tasks;

namespace Prometheus.Tests
{
    [TestClass]
    public sealed class MetricInitializationTests
    {
        private static HistogramConfiguration NewHistogramConfiguration() => new HistogramConfiguration
        {
            // This results in 4 metrics - sum, count, 1.0, +Inf
            Buckets = new[] { 1.0 }
        };

        private static SummaryConfiguration NewSummaryConfiguration() => new SummaryConfiguration
        {
            // This results in 3 metrics - sum, count, 0.1
            Objectives = new[]
            {
                new QuantileEpsilonPair(0.1, 0.05)
            }
        };

        #region Unlabelled logic
        [TestMethod]
        public async Task CreatingUnlabelledMetric_WithoutObservingAnyData_ExportsImmediately()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var summaryConfig = NewSummaryConfiguration();
            var histogramConfig = NewHistogramConfiguration();

            var gauge = factory.CreateGauge("gauge", "", new GaugeConfiguration
            {
            });
            var counter = factory.CreateCounter("counter", "", new CounterConfiguration
            {
            });
            var summary = factory.CreateSummary("summary", "", summaryConfig);
            var histogram = factory.CreateHistogram("histogram", "", histogramConfig);
            // 4 families with 9 metrics total.

            var serializer = Substitute.For<IMetricsSerializer>();
            await registry.CollectAndSerializeAsync(serializer, default);

            // Without touching any metrics, there should be output for all because default config publishes immediately.

            await serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclarationAsync(default, default);
            await serializer.ReceivedWithAnyArgs(9).WriteMetricAsync(default, default, default);
        }

        [TestMethod]
        public async Task CreatingUnlabelledMetric_WithInitialValueSuppression_ExportsNothingByDefault()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var sumamryConfig = NewSummaryConfiguration();
            sumamryConfig.SuppressInitialValue = true;

            var histogramConfig = NewHistogramConfiguration();
            histogramConfig.SuppressInitialValue = true;

            var gauge = factory.CreateGauge("gauge", "", new GaugeConfiguration
            {
                SuppressInitialValue = true
            });
            var counter = factory.CreateCounter("counter", "", new CounterConfiguration
            {
                SuppressInitialValue = true
            });
            var summary = factory.CreateSummary("summary", "", sumamryConfig);
            var histogram = factory.CreateHistogram("histogram", "", histogramConfig);
            // 4 families with 9 metrics total.

            var serializer = Substitute.For<IMetricsSerializer>();
            await registry.CollectAndSerializeAsync(serializer, default);

            // There is a family for each of the above, in each family we expect to see 0 metrics.
            await serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclarationAsync(default, default);
            await serializer.DidNotReceiveWithAnyArgs().WriteMetricAsync(default, default, default);
        }

        [TestMethod]
        public async Task CreatingUnlabelledMetric_WithInitialValueSuppression_ExportsAfterValueChange()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var sumamryConfig = NewSummaryConfiguration();
            sumamryConfig.SuppressInitialValue = true;

            var histogramConfig = NewHistogramConfiguration();
            histogramConfig.SuppressInitialValue = true;

            var gauge = factory.CreateGauge("gauge", "", new GaugeConfiguration
            {
                SuppressInitialValue = true
            });
            var counter = factory.CreateCounter("counter", "", new CounterConfiguration
            {
                SuppressInitialValue = true
            });
            var summary = factory.CreateSummary("summary", "", sumamryConfig);
            var histogram = factory.CreateHistogram("histogram", "", histogramConfig);
            // 4 families with 9 metrics total.

            gauge.Set(123);
            counter.Inc();
            summary.Observe(123);
            histogram.Observe(31);

            var serializer = Substitute.For<IMetricsSerializer>();
            await registry.CollectAndSerializeAsync(serializer, default);

            // Even though suppressed, they all now have values so should all be published.
            await serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclarationAsync(default, default);
            await serializer.ReceivedWithAnyArgs(9).WriteMetricAsync(default, default, default);
        }

        [TestMethod]
        public async Task CreatingUnlabelledMetric_WithInitialValueSuppression_ExportsAfterPublish()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var sumamryConfig = NewSummaryConfiguration();
            sumamryConfig.SuppressInitialValue = true;

            var histogramConfig = NewHistogramConfiguration();
            histogramConfig.SuppressInitialValue = true;

            var gauge = factory.CreateGauge("gauge", "", new GaugeConfiguration
            {
                SuppressInitialValue = true
            });
            var counter = factory.CreateCounter("counter", "", new CounterConfiguration
            {
                SuppressInitialValue = true
            });
            var summary = factory.CreateSummary("summary", "", sumamryConfig);
            var histogram = factory.CreateHistogram("histogram", "", histogramConfig);
            // 4 families with 9 metrics total.

            gauge.Publish();
            counter.Publish();
            summary.Publish();
            histogram.Publish();

            var serializer = Substitute.For<IMetricsSerializer>();
            await registry.CollectAndSerializeAsync(serializer, default);

            // Even though suppressed, they were all explicitly published.
            await serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclarationAsync(default, default);
            await serializer.ReceivedWithAnyArgs(9).WriteMetricAsync(default, default, default);
        }
        #endregion

        #region Labelled logic
        [TestMethod]
        public async Task CreatingLabelledMetric_WithoutObservingAnyData_ExportsImmediately()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var sumamryConfig = NewSummaryConfiguration();
            sumamryConfig.LabelNames = new[] { "foo" };

            var histogramConfig = NewHistogramConfiguration();
            histogramConfig.LabelNames = new[] { "foo" };

            var gauge = factory.CreateGauge("gauge", "", new GaugeConfiguration
            {
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");
            var counter = factory.CreateCounter("counter", "", new CounterConfiguration
            {
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");
            var summary = factory.CreateSummary("summary", "", sumamryConfig).WithLabels("bar");
            var histogram = factory.CreateHistogram("histogram", "", histogramConfig).WithLabels("bar");
            // 4 families with 9 metrics total.

            var serializer = Substitute.For<IMetricsSerializer>();
            await registry.CollectAndSerializeAsync(serializer, default);

            // Metrics are published as soon as label values are defined.
            await serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclarationAsync(default, default);
            await serializer.ReceivedWithAnyArgs(9).WriteMetricAsync(default, default, default);
        }

        [TestMethod]
        public async Task CreatingLabelledMetric_WithInitialValueSuppression_ExportsNothingByDefault()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var sumamryConfig = NewSummaryConfiguration();
            sumamryConfig.SuppressInitialValue = true;
            sumamryConfig.LabelNames = new[] { "foo" };

            var histogramConfig = NewHistogramConfiguration();
            histogramConfig.SuppressInitialValue = true;
            histogramConfig.LabelNames = new[] { "foo" };

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
            var summary = factory.CreateSummary("summary", "", sumamryConfig).WithLabels("bar");
            var histogram = factory.CreateHistogram("histogram", "", histogramConfig).WithLabels("bar");
            // 4 families with 9 metrics total.

            var serializer = Substitute.For<IMetricsSerializer>();
            await registry.CollectAndSerializeAsync(serializer, default);

            // Publishing was suppressed.
            await serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclarationAsync(default, default);
            await serializer.DidNotReceiveWithAnyArgs().WriteMetricAsync(default, default, default);
        }

        [TestMethod]
        public async Task CreatingLabelledMetric_WithInitialValueSuppression_ExportsAfterValueChange()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var sumamryConfig = NewSummaryConfiguration();
            sumamryConfig.SuppressInitialValue = true;
            sumamryConfig.LabelNames = new[] { "foo" };

            var histogramConfig = NewHistogramConfiguration();
            histogramConfig.SuppressInitialValue = true;
            histogramConfig.LabelNames = new[] { "foo" };

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
            var summary = factory.CreateSummary("summary", "", sumamryConfig).WithLabels("bar");
            var histogram = factory.CreateHistogram("histogram", "", histogramConfig).WithLabels("bar");
            // 4 families with 9 metrics total.

            gauge.Set(123);
            counter.Inc();
            summary.Observe(123);
            histogram.Observe(31);

            var serializer = Substitute.For<IMetricsSerializer>();
            await registry.CollectAndSerializeAsync(serializer, default);

            // Metrics are published because value was set.
            await serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclarationAsync(default, default);
            await serializer.ReceivedWithAnyArgs(9).WriteMetricAsync(default, default, default);
        }

        [TestMethod]
        public async Task CreatingLabelledMetric_WithInitialValueSuppression_ExportsAfterPublish()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var sumamryConfig = NewSummaryConfiguration();
            sumamryConfig.SuppressInitialValue = true;
            sumamryConfig.LabelNames = new[] { "foo" };

            var histogramConfig = NewHistogramConfiguration();
            histogramConfig.SuppressInitialValue = true;
            histogramConfig.LabelNames = new[] { "foo" };

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
            var summary = factory.CreateSummary("summary", "", sumamryConfig).WithLabels("bar");
            var histogram = factory.CreateHistogram("histogram", "", histogramConfig).WithLabels("bar");
            // 4 families with 9 metrics total.

            gauge.Publish();
            counter.Publish();
            summary.Publish();
            histogram.Publish();

            var serializer = Substitute.For<IMetricsSerializer>();
            await registry.CollectAndSerializeAsync(serializer, default);

            // Metrics are published because of explicit publish.
            await serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclarationAsync(default, default);
            await serializer.ReceivedWithAnyArgs(9).WriteMetricAsync(default, default, default);
        }

        [TestMethod]
        public async Task CreatingLabelledMetric_AndUnpublishingAfterObservingData_DoesNotExport()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var counter = factory.CreateCounter("counter", "", new CounterConfiguration
            {
                LabelNames = new[] { "foo" }
            }).WithLabels("bar");

            counter.Inc();
            counter.Unpublish();

            var serializer = Substitute.For<IMetricsSerializer>();
            await registry.CollectAndSerializeAsync(serializer, default);

            await serializer.ReceivedWithAnyArgs(1).WriteFamilyDeclarationAsync(default, default);
            await serializer.ReceivedWithAnyArgs(0).WriteMetricAsync(default, default, default);
        }
        #endregion

        #region Relation between labelled and unlabelled
        [TestMethod]
        public async Task CreatingLabelledMetric_WithoutObservingAnyData_DoesNotExportUnlabelled()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var summaryConfig = NewSummaryConfiguration();
            summaryConfig.LabelNames = new[] { "labelname" };
            var histogramConfig = NewHistogramConfiguration();
            histogramConfig.LabelNames = new[] { "labelname" };

            var gauge = factory.CreateGauge("gauge", "", new GaugeConfiguration
            {
                LabelNames = new[] { "labelname" }
            });
            var counter = factory.CreateCounter("counter", "", new CounterConfiguration
            {
                LabelNames = new[] { "labelname" }
            });
            var summary = factory.CreateSummary("summary", "", summaryConfig);
            var histogram = factory.CreateHistogram("histogram", "", histogramConfig);
            // 4 families with 9 metrics total.

            var serializer = Substitute.For<IMetricsSerializer>();
            await registry.CollectAndSerializeAsync(serializer, default);

            // Family for each of the above, in each is 0 metrics.
            await serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclarationAsync(default, default);
            await serializer.DidNotReceiveWithAnyArgs().WriteMetricAsync(default, default, default);
        }

        [TestMethod]
        public async Task CreatingLabelledMetric_AfterObservingLabelledData_DoesNotExportUnlabelled()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var summaryConfig = NewSummaryConfiguration();
            summaryConfig.LabelNames = new[] { "labelname" };
            var histogramConfig = NewHistogramConfiguration();
            histogramConfig.LabelNames = new[] { "labelname" };

            var gauge = factory.CreateGauge("gauge", "", new GaugeConfiguration
            {
                LabelNames = new[] { "labelname" }
            });
            var counter = factory.CreateCounter("counter", "", new CounterConfiguration
            {
                LabelNames = new[] { "labelname" }
            });
            var summary = factory.CreateSummary("summary", "", summaryConfig);
            var histogram = factory.CreateHistogram("histogram", "", histogramConfig);
            // 4 families with 9 metrics total.

            // Touch some labelled metrics.
            gauge.WithLabels("labelvalue").Inc();
            counter.WithLabels("labelvalue").Inc();
            summary.WithLabels("labelvalue").Observe(123);
            histogram.WithLabels("labelvalue").Observe(123);

            var serializer = Substitute.For<IMetricsSerializer>();
            await registry.CollectAndSerializeAsync(serializer, default);

            // Family for each of the above, in each is 4 metrics (labelled only).
            await serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclarationAsync(default, default);
            await serializer.ReceivedWithAnyArgs(9).WriteMetricAsync(default, default, default);

            // Only after touching unlabelled do they get published.
            gauge.Inc();
            counter.Inc();
            summary.Observe(123);
            histogram.Observe(123);

            serializer.ClearReceivedCalls();
            await registry.CollectAndSerializeAsync(serializer, default);

            // Family for each of the above, in each is 8 metrics (unlabelled+labelled).
            await serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclarationAsync(default, default);
            await serializer.ReceivedWithAnyArgs(9 * 2).WriteMetricAsync(default, default, default);
        }
        #endregion

        [TestMethod]
        public void RemovingLabeledInstance_ThenRecreatingIt_CreatesIndependentInstance()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var counter = factory.CreateCounter("counter", "", new CounterConfiguration
            {
                LabelNames = new[] { "foo" }
            });

            var bar1 = counter.WithLabels("bar");
            bar1.Inc();

            Assert.AreEqual(1, bar1.Value);
            bar1.Remove();

            // The new instance after the old one was removed must be independent.
            var bar2 = counter.WithLabels("bar");
            Assert.AreEqual(0, bar2.Value);
        }
    }
}
