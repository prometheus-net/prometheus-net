using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

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
        public void CreatingUnlabelledMetric_WithoutObservingAnyData_ExportsImmediately()
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
            registry.CollectAndSerialize(serializer);

            // Without touching any metrics, there should be output for all because default config publishes immediately.

            serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclaration(default);
            serializer.ReceivedWithAnyArgs(9).WriteMetric(default, default);
        }

        [TestMethod]
        public void CreatingUnlabelledMetric_WithInitialValueSuppression_ExportsNothingByDefault()
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
            registry.CollectAndSerialize(serializer);

            // There is a family for each of the above, in each family we expect to see 0 metrics.
            serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclaration(default);
            serializer.DidNotReceiveWithAnyArgs().WriteMetric(default, default);
        }

        [TestMethod]
        public void CreatingUnlabelledMetric_WithInitialValueSuppression_ExportsAfterValueChange()
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
            registry.CollectAndSerialize(serializer);

            // Even though suppressed, they all now have values so should all be published.
            serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclaration(default);
            serializer.ReceivedWithAnyArgs(9).WriteMetric(default, default);
        }

        [TestMethod]
        public void CreatingUnlabelledMetric_WithInitialValueSuppression_ExportsAfterPublish()
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
            registry.CollectAndSerialize(serializer);

            // Even though suppressed, they were all explicitly published.
            serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclaration(default);
            serializer.ReceivedWithAnyArgs(9).WriteMetric(default, default);
        }
        #endregion

        #region Labelled logic
        [TestMethod]
        public void CreatingLabelledMetric_WithoutObservingAnyData_ExportsImmediately()
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
            registry.CollectAndSerialize(serializer);

            // Metrics are published as soon as label values are defined.
            serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclaration(default);
            serializer.ReceivedWithAnyArgs(9).WriteMetric(default, default);
        }

        [TestMethod]
        public void CreatingLabelledMetric_WithInitialValueSuppression_ExportsNothingByDefault()
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
            registry.CollectAndSerialize(serializer);

            // Publishing was suppressed.
            serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclaration(default);
            serializer.DidNotReceiveWithAnyArgs().WriteMetric(default, default);
        }

        [TestMethod]
        public void CreatingLabelledMetric_WithInitialValueSuppression_ExportsAfterValueChange()
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
            registry.CollectAndSerialize(serializer);

            // Metrics are published because value was set.
            serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclaration(default);
            serializer.ReceivedWithAnyArgs(9).WriteMetric(default, default);
        }

        [TestMethod]
        public void CreatingLabelledMetric_WithInitialValueSuppression_ExportsAfterPublish()
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
            registry.CollectAndSerialize(serializer);

            // Metrics are published because of explicit publish.
            serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclaration(default);
            serializer.ReceivedWithAnyArgs(9).WriteMetric(default, default);
        }
        #endregion

        #region Relation between labelled and unlabelled
        [TestMethod]
        public void CreatingLabelledMetric_WithoutObservingAnyData_DoesNotExportUnlabelled()
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
            registry.CollectAndSerialize(serializer);

            // Family for each of the above, in each is 0 metrics.
            serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclaration(default);
            serializer.DidNotReceiveWithAnyArgs().WriteMetric(default, default);
        }

        [TestMethod]
        public void CreatingLabelledMetric_AfterObservingLabelledData_DoesNotExportUnlabelled()
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
            registry.CollectAndSerialize(serializer);

            // Family for each of the above, in each is 4 metrics (labelled only).
            serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclaration(default);
            serializer.ReceivedWithAnyArgs(9).WriteMetric(default, default);

            // Only after touching unlabelled do they get published.
            gauge.Inc();
            counter.Inc();
            summary.Observe(123);
            histogram.Observe(123);

            serializer.ClearReceivedCalls();
            registry.CollectAndSerialize(serializer);

            // Family for each of the above, in each is 8 metrics (unlabelled+labelled).
            serializer.ReceivedWithAnyArgs(4).WriteFamilyDeclaration(default);
            serializer.ReceivedWithAnyArgs(9 * 2).WriteMetric(default, default);
        }
        #endregion
    }
}
