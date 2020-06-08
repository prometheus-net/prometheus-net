using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Prometheus.Tests
{
    [TestClass]
    public class CounterTests
    {
        private CollectorRegistry _registry;
        private MetricFactory _metrics;

        public CounterTests()
        {
            _registry = Metrics.NewCustomRegistry();
            _metrics = Metrics.WithCustomRegistry(_registry);
        }

        [TestMethod]
        public void IncTo_IncrementsButDoesNotDecrement()
        {
            var counter = _metrics.CreateCounter("xxx", "xxx");

            counter.IncTo(100);
            Assert.AreEqual(100, counter.Value);

            counter.IncTo(100);
            Assert.AreEqual(100, counter.Value);

            counter.IncTo(10);
            Assert.AreEqual(100, counter.Value);
        }
    }
}
