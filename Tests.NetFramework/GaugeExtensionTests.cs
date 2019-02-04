using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Prometheus.Tests
{
    [TestClass]
    public sealed class GaugeExtensionTests
    {
        [TestMethod]
        public void SetToCurrentTimeUtc_SetsToCorrectValue()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var gauge = factory.CreateGauge("xxx", "");

            gauge.SetToCurrentTimeUtc();

            // Approximate.
            var expectedValue = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            const double toleranceSeconds = 10;
            Assert.IsTrue(Math.Abs(expectedValue - gauge.Value) < toleranceSeconds);
        }
    }
}
