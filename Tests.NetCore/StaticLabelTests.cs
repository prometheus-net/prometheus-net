using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prometheus.Tests
{
    [TestClass]
    public sealed class StaticLabelTests
    {
        [TestMethod]
        public void StaticLabels_EmittedForAllLevels()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            registry.SetStaticLabels(new Dictionary<string, string>
            {
                { "registryLabel", "registryLabelValue" }
            });

            var registryLabel = factory.CreateCounter("foo1", "");

            var registryAndMetricLabel = factory.CreateCounter("foo2", "", new CounterConfiguration
            {
                StaticLabels = new Dictionary<string, string>
                {
                    { "metricLabel", "metricLabelValue" }
                }
            });

            var registryAndMetricAndInstanceLabel = factory.CreateCounter("foo3", "", new CounterConfiguration
            {
                StaticLabels = new Dictionary<string, string>
                {
                    { "metricLabel", "metricLabelValue" }
                },
                LabelNames = new[] { "instanceLabel" }
            });

            var labels = registryLabel.Unlabelled.FlattenedLabels;
            Assert.AreEqual(1, labels.Count);
            CollectionAssert.Contains(labels.Names, "registryLabel");
            CollectionAssert.Contains(labels.Values, "registryLabelValue");

            labels = registryAndMetricLabel.Unlabelled.FlattenedLabels;
            Assert.AreEqual(2, labels.Count);
            CollectionAssert.Contains(labels.Names, "registryLabel");
            CollectionAssert.Contains(labels.Values, "registryLabelValue");
            CollectionAssert.Contains(labels.Names, "metricLabel");
            CollectionAssert.Contains(labels.Values, "metricLabelValue");

            var instance = registryAndMetricAndInstanceLabel.WithLabels("instanceLabelValue");
            labels = instance.FlattenedLabels;
            Assert.AreEqual(3, labels.Count);
            CollectionAssert.Contains(labels.Names, "registryLabel");
            CollectionAssert.Contains(labels.Values, "registryLabelValue");
            CollectionAssert.Contains(labels.Names, "metricLabel");
            CollectionAssert.Contains(labels.Values, "metricLabelValue");
            CollectionAssert.Contains(labels.Names, "instanceLabel");
            CollectionAssert.Contains(labels.Values, "instanceLabelValue");
        }

        [TestMethod]
        public void LabelCollision_IsNotAllowed()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            registry.SetStaticLabels(new Dictionary<string, string>
            {
                { "registryLabel", "registryLabelValue" }
            });

            var registryLabel = factory.CreateCounter("foo1", "");

            var registryAndMetricLabel = factory.CreateCounter("foo2", "", new CounterConfiguration
            {
                StaticLabels = new Dictionary<string, string>
                {
                    { "metricLabel", "metricLabelValue" }
                }
            });

            // Static label (registry) and instance label with same name -> error.
            Assert.ThrowsException<InvalidOperationException>(() => factory.CreateGauge("test1", "", new GaugeConfiguration
            {
                LabelNames = new[] { "registryLabel" }
            }));

            // Static label (metric) and instance label with same name -> error.
            Assert.ThrowsException<InvalidOperationException>(() => factory.CreateGauge("test2", "", new GaugeConfiguration
            {
                StaticLabels = new Dictionary<string, string>
                {
                    { "metricLabel", "metricLabelValue" }
                },
                LabelNames = new[] { "metricLabel" }
            }));

            // Static label (registry) and static label (metric) with same name -> error.
            Assert.ThrowsException<InvalidOperationException>(() => factory.CreateGauge("test2", "", new GaugeConfiguration
            {
                StaticLabels = new Dictionary<string, string>
                {
                    { "registryLabel", "" }
                }
            }));
        }

        [TestMethod]
        public void SetRegistryLabelsTwice_IsNotAllowed()
        {
            var registry = Metrics.NewCustomRegistry();

            registry.SetStaticLabels(new Dictionary<string, string>
            {
                { "registryLabel", "registryLabelValue" }
            });

            Assert.ThrowsException<InvalidOperationException>(() => registry.SetStaticLabels(new Dictionary<string, string>
            {
                { "registryLabel", "registryLabelValue" }
            }));
        }

        [TestMethod]
        public async Task SetRegistryLabelsAfterFirstCollect_IsNotAllowed()
        {
            var registry = Metrics.NewCustomRegistry();

            await registry.CollectAndExportAsTextAsync(new MemoryStream());

            Assert.ThrowsException<InvalidOperationException>(() => registry.SetStaticLabels(new Dictionary<string, string>
            {
                { "registryLabel", "registryLabelValue" }
            }));
        }

        [TestMethod]
        public void SetRegistryLabelsAfterMetricAdded_IsNotAllowed()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            factory.CreateGauge("foo", "");

            Assert.ThrowsException<InvalidOperationException>(() => registry.SetStaticLabels(new Dictionary<string, string>
            {
                { "registryLabel", "registryLabelValue" }
            }));
        }
    }
}
