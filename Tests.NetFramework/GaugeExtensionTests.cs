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

        [TestMethod]
        public void IncToCurrentTimeUtc_SetsToCorrectValue()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var gauge = factory.CreateGauge("xxx", "");

            // Starts from 0, becomes "now"
            gauge.IncToCurrentTimeUtc();

            // Approximate.
            var expectedValue = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            const double toleranceSeconds = 10;
            Assert.IsTrue(Math.Abs(expectedValue - gauge.Value) < toleranceSeconds);

            var bigValue = expectedValue + 99999;
            gauge.Set(bigValue);

            // Should remain "big"
            gauge.IncToCurrentTimeUtc();

            Assert.AreEqual(bigValue, gauge.Value);
        }
    }
}
