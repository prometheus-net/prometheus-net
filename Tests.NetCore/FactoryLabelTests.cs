using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Prometheus.Tests
{
    [TestClass]
    public sealed class FactoryLabelTests
    {
        private CollectorRegistry _registry;
        private MetricFactory _metrics;

        public FactoryLabelTests()
        {
            _registry = Metrics.NewCustomRegistry();
            _metrics = Metrics.WithCustomRegistry(_registry);
        }

        [TestMethod]
        public async Task WithLabels_WithSomeLabels_AddsLabelsToCreatedMetrics()
        {
            var canary1 = "gvst5um08ouym";
            var canary2 = "dr54n6bbu4";
            var canary3 = "ncru6jnhcr66ncr";
            var canary4 = "cnu567nud65";

            var factory = _metrics.WithLabels(new Dictionary<string, string>
            {
                { canary1, canary2 },
                { canary3, canary4 }
            });

            var counter = factory.CreateCounter("test_counter", "");
            counter.Inc();

            var serialized = await _registry.CollectAndSerializeToStringAsync();

            StringAssert.Contains(serialized, canary1.ToString());
            StringAssert.Contains(serialized, canary2.ToString());
            StringAssert.Contains(serialized, canary3.ToString());
            StringAssert.Contains(serialized, canary4.ToString());
        }

        [TestMethod]
        public void WithLabels_WithFactoryAndMetricConflict_WillThrowWhenCreatingMetric()
        {
            var factory = _metrics.WithLabels(new Dictionary<string, string>
            {
                { "foo", "bar" }
            });

            Assert.ThrowsException<InvalidOperationException>(delegate
            {
                factory.CreateCounter("test_counter", "", new CounterConfiguration
                {
                    LabelNames = new[] { "foo" }
                });
            });
        }

        [TestMethod]
        public void WithLabels_WithFactoryAndRegistryConflict_WillThrowWhenCreatingFactory()
        {
            _registry.SetStaticLabels(new Dictionary<string, string>
            {
                { "foo", "bar" }
            });

            Assert.ThrowsException<InvalidOperationException>(delegate
            {
                _metrics.WithLabels(new Dictionary<string, string>
                {
                    { "foo", "bar" }
                });
            });
        }

        [TestMethod]
        public void WithLabels_WithEmptyLabelSet_IsNoop()
        {
            var counter1 = _metrics.CreateCounter("test_counter", "");

            var factory = _metrics.WithLabels(new Dictionary<string, string>());

            var counter2 = factory.CreateCounter("test_counter", "");

            // We expect both counters to increment the same value because they should have the same labels.
            // It is not strictly required that they be the same instance (they should be but that's not the point of this test).
            counter1.Inc();
            counter2.Inc();

            Assert.AreEqual(2, counter1.Value);
        }
    }
}
