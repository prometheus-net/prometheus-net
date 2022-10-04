using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
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

            var registryAndInstanceLabel = factory.CreateCounter("foo3", "", new[] { "instanceLabel" });

            var labels = registryLabel.Unlabelled.FlattenedLabels;
            Assert.AreEqual(1, labels.Length);
            Assert.IsTrue(labels.Names.Contains("registryLabel"));
            Assert.IsTrue(labels.Values.Contains("registryLabelValue"));

            var instance = registryAndInstanceLabel.WithLabels("instanceLabelValue");
            labels = instance.FlattenedLabels;
            Assert.AreEqual(2, labels.Length);
            Assert.IsTrue(labels.Names.Contains("registryLabel"));
            Assert.IsTrue(labels.Values.Contains("registryLabelValue"));
            Assert.IsTrue(labels.Names.Contains("instanceLabel"));
            Assert.IsTrue(labels.Values.Contains("instanceLabelValue"));
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

            // Static label (registry) and instance label with same name -> error.
            Assert.ThrowsException<InvalidOperationException>(() => factory.CreateGauge("test1", "", new[] { "registryLabel" }));

            var factoryLabel = factory.WithLabels(new Dictionary<string, string>
            {
                { "registryLabel", "otherValue" }
            });

            // Static label (registry) and factory label with same name -> error.
            Assert.ThrowsException<InvalidOperationException>(() => factory.CreateGauge("test1", "", new[] { "registryLabel" }));

            // Factory label and instance label with same name -> error.
            var registry2 = Metrics.NewCustomRegistry();
            var factory2 = Metrics.WithCustomRegistry(registry2).WithLabels(new Dictionary<string, string>
            {
                { "factoryLabel", "value" }
            });

            Assert.ThrowsException<InvalidOperationException>(() => factory2.CreateGauge("test1", "", new[] { "factoryLabel" }));
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
