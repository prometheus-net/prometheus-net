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

            // All increments should be seen by all viewpoints to proves they are the same counter.
            Assert.AreEqual(3, counter1.Value);

            using (var lease = counterHandle.AcquireLease(out var instance))
                Assert.AreEqual(3, instance.Value);

            Assert.AreEqual(3, counterHandle.WithExtendLifetimeOnUse().Unlabelled.Value);
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

            using (handle.AcquireLease(out var instance1))
            {
                instance1.Inc();

                using (handle.AcquireLease(out var instance2))
                {
                    instance2.Inc();

                    await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and start expiring.

                    delayer.BreakAllDelays();
                    await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and finish expiring.

                    // 2 leases remain - should not have expired yet. Check with a fresh copy from the root registry.
                    Assert.AreEqual(2, _metrics.CreateCounter(MetricName, "").Value);
                }

                await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and start expiring.

                delayer.BreakAllDelays();
                await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and finish expiring.

                // 1 lease remains - should not have expired yet. Check with a fresh copy from the root registry.
                Assert.AreEqual(2, _metrics.CreateCounter(MetricName, "").Value);
            }

            await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and start expiring.

            delayer.BreakAllDelays();
            await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and finish expiring.

            // 0 leases remains - should have expired. Check with a fresh copy from the root registry.
            Assert.AreEqual(0, _metrics.CreateCounter(MetricName, "").Value);
        }

        private sealed class CancelDetectingDelayer : IDelayer
        {
            public int CancelCount { get; private set; }

            private readonly object _lock = new();

            public Task Delay(TimeSpan duration)
            {
                throw new NotSupportedException();
            }

            public async Task Delay(TimeSpan duration, CancellationToken cancel)
            {
                try
                {
                    await Task.Delay(-1, cancel);
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                {
                    lock (_lock)
                        CancelCount++;
                }
            }
        }

        [TestMethod]
        public async Task AutoLeaseMetric_OnWrite_RefreshesLease()
        {
            var handle = _expiringMetrics.CreateCounter(MetricName, "");

            // We detect lease refresh by the existing delay being cancelled (and a new delay being made).
            var delayer = new CancelDetectingDelayer();
            ((ManagedLifetimeCounter)handle).Delayer = delayer;

            // At the start, no expiration timer is running (it will only start with the first lease being released).

            var counter = handle.WithExtendLifetimeOnUse();

            // Acquires + releases first lease. Expiration timer starts.
            counter.Unlabelled.Inc();
            await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to trigger any potential expiration timer reset.

            Assert.AreEqual(0, delayer.CancelCount);

            // Acquires + releases 2nd lease. Expiration timer restarts.
            counter.Unlabelled.Inc();
            await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to trigger any potential expiration timer reset.

            Assert.AreEqual(1, delayer.CancelCount);
        }

        [TestMethod]
        public async Task AutoLeaseMetric_OnRead_DoesNotRefreshLease()
        {
            var handle = _expiringMetrics.CreateCounter(MetricName, "");

            // We detect lease refresh by the existing delay being cancelled (and a new delay being made).
            var delayer = new CancelDetectingDelayer();
            ((ManagedLifetimeCounter)handle).Delayer = delayer;

            // At the start, no expiration timer is running (it will only start with the first lease being released).

            var counter = handle.WithExtendLifetimeOnUse();

            // Acquires + releases first lease. Expiration timer starts.
            counter.Unlabelled.Inc();
            await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to trigger any potential expiration timer reset.

            Assert.AreEqual(0, delayer.CancelCount);

            // Does not acquire a lease. No effect on lifetime. Expiration timer keeps ticking without cancellation.
            var value = counter.Unlabelled.Value;
            await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to trigger any potential expiration timer reset.

            Assert.AreEqual(0, delayer.CancelCount);
        }
    }
}
