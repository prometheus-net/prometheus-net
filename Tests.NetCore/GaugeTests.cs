using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Prometheus.Tests
{
    [TestClass]
    public class GaugeTests
    {
        private CollectorRegistry _registry;
        private MetricFactory _metrics;

        public GaugeTests()
        {
            _registry = Metrics.NewCustomRegistry();
            _metrics = Metrics.WithCustomRegistry(_registry);
        }

        [TestMethod]
        public void IncTo_IncrementsButDoesNotDecrement()
        {
            var gauge = _metrics.CreateGauge("xxx", "xxx");

            gauge.IncTo(100);
            Assert.AreEqual(100, gauge.Value);

            gauge.IncTo(100);
            Assert.AreEqual(100, gauge.Value);

            gauge.IncTo(10);
            Assert.AreEqual(100, gauge.Value);
        }

        [TestMethod]
        public void DecTo_DecrementsButDoesNotIncrement()
        {
            var gauge = _metrics.CreateGauge("xxx", "xxx");
            gauge.Set(999);

            gauge.DecTo(100);
            Assert.AreEqual(100, gauge.Value);

            gauge.DecTo(100);
            Assert.AreEqual(100, gauge.Value);

            gauge.DecTo(500);
            Assert.AreEqual(100, gauge.Value);
        }
    }
}
