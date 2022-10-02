using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus.Tests
{
    [TestClass]
    public sealed class MetricExpirationTests
    {
        private CollectorRegistry _registry;
        private MetricFactory _metrics;
        private IManagedLifetimeMetricFactory _expiringMetrics;

        public MetricExpirationTests()
        {
            _registry = Metrics.NewCustomRegistry();
            _metrics = Metrics.WithCustomRegistry(_registry);
            // We set the expiration to very high count because we only want things to expire when we trigger it manually (via BreakableDelayer).
            _expiringMetrics = _metrics.WithManagedLifetime(expiresAfter: TimeSpan.FromHours(24));
        }

        private const string MetricName = "foo_bar";

        // Lease expiration starts and stops asynchronously, so we cannot with 100% reliability detect it.
        // To try detect it with good-enough reliability, we simply sleep a bit at any point where background logic may want to act.
        // This has a slight dependency on the performance of the PC executing the tests - maybe not ideal long term strategy but what can you do.
        private static readonly TimeSpan WaitForAsyncActionSleepTime = TimeSpan.FromSeconds(0.1);

        [TestMethod]
        public void ManagedLifetimeMetric_IsSameMetricAsNormalMetric()
        {
            var counter1 = _metrics.CreateCounter(MetricName, "");
            var counterHandle = _expiringMetrics.CreateCounter(MetricName, "");

            counter1.Inc();

            using (var lease = counterHandle.AcquireLease(out var instance))
                instance.Inc();

            counterHandle.WithExtendLifetimeOnUse().Unlabelled.Inc();

            counterHandle.WithLease(c => c.Inc());

            // All increments should be seen by all viewpoints to proves they are the same counter.
            Assert.AreEqual(4, counter1.Value);

            using (var lease = counterHandle.AcquireLease(out var instance))
                Assert.AreEqual(4, instance.Value);

            // Cannot read value of auto-leasing metric, so we just check the other 2 saw all 3 writes.
        }

        [TestMethod]
        public void ManagedLifetimeMetric_MultipleHandlesFromSameFactory_AreSameHandle()
        {
            var handle1 = _expiringMetrics.CreateCounter(MetricName, "");
            var handle2 = _expiringMetrics.CreateCounter(MetricName, "");

            Assert.AreSame(handle1, handle2);
        }

        [TestMethod]
        public void ManagedLifetimeMetric_ViaDifferentFactories_IsSameMetric()
        {
            var handle1 = _expiringMetrics.CreateCounter(MetricName, "");

            var expiringMetrics2 = _metrics.WithManagedLifetime(expiresAfter: TimeSpan.FromHours(24));
            var handle2 = expiringMetrics2.CreateCounter(MetricName, "");

            using (var lease = handle1.AcquireLease(out var instance))
                instance.Inc();

            using (var lease = handle2.AcquireLease(out var instance))
                instance.Inc();

            // Both increments should be seen by both counters, this proves they are the same counter.
            using (var lease = handle1.AcquireLease(out var instance))
                Assert.AreEqual(2, instance.Value);

            using (var lease = handle2.AcquireLease(out var instance))
                Assert.AreEqual(2, instance.Value);
        }

        [TestMethod]
        public async Task ManagedLifetimeMetric_ExpiresOnlyAfterAllLeasesReleased()
        {
            var handle = _expiringMetrics.CreateCounter(MetricName, "");

            // We break delays on demand to force any expiring-eligible metrics to expire.
            var delayer = new BreakableDelayer();
            ((ManagedLifetimeCounter)handle).Delayer = delayer;

            // We detect expiration by the value having been reset when we try allocate the counter again.
            // We break 2 delays on every use, to ensure that the expiration logic has enough iterations to make up its mind.

            using (handle.AcquireLease(out var instance1))
            {
                instance1.Inc();

                using (handle.AcquireLease(out var instance2))
                {
                    instance2.Inc();

                    await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and start expiring.

                    delayer.BreakAllDelays();
                    await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and finish expiring.
                    delayer.BreakAllDelays();
                    await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and finish expiring.

                    // 2 leases remain - should not have expired yet. Check with a fresh copy from the root registry.
                    Assert.AreEqual(2, _metrics.CreateCounter(MetricName, "").Value);
                }

                await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and start expiring.

                delayer.BreakAllDelays();
                await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and finish expiring.
                delayer.BreakAllDelays();
                await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and finish expiring.

                // 1 lease remains - should not have expired yet. Check with a fresh copy from the root registry.
                Assert.AreEqual(2, _metrics.CreateCounter(MetricName, "").Value);
            }

            await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and start expiring.

            delayer.BreakAllDelays();
            await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and finish expiring.
            delayer.BreakAllDelays();
            await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and finish expiring.

            // 0 leases remains - should have expired. Check with a fresh copy from the root registry.
            Assert.AreEqual(0, _metrics.CreateCounter(MetricName, "").Value);
        }
    }
}
