using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus.Advanced;
using System.Linq;

namespace Prometheus.Tests
{
    [TestClass]
    public sealed class MetricLabelTests
    {
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
    }
}
