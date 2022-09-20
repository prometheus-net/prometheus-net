using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace Prometheus.Tests
{
    [TestClass]
    public sealed class CounterExtensionTests
    {
        [TestMethod]
        public void CountExceptions_WithNoException_DoesNotCount()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var counter = factory.CreateCounter("xxx", "");

            counter.CountExceptions(() => { });

            Assert.AreEqual(0, counter.Value);
        }

        [TestMethod]
        public void CountExceptions_WithExceptionAndNoFilter_CountsAndRethrows()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var counter = factory.CreateCounter("xxx", "");

            Assert.ThrowsException<OverflowException>(() => counter.CountExceptions(() => throw new OverflowException()));

            Assert.AreEqual(1, counter.Value);
        }

        [TestMethod]
        public void CountExceptions_WithExceptionNotMatchingFilter_DoesNotCount()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var counter = factory.CreateCounter("xxx", "");

            Assert.ThrowsException<OverflowException>(() => counter.CountExceptions(() => throw new OverflowException(), ex => false));

            Assert.AreEqual(0, counter.Value);
        }

        [TestMethod]
        public async Task CountExceptionsAsync_WithException_CountsAndRethrows()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var counter = factory.CreateCounter("xxx", "");

            await Assert.ThrowsExceptionAsync<OverflowException>(async () => await counter.CountExceptionsAsync(async () =>
            {
                await Task.Yield();
                throw new OverflowException();
            }));

            Assert.AreEqual(1, counter.Value);
        }
    }
}
