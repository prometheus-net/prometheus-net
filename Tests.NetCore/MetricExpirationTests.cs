using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            var counterHandle = _expiringMetrics.CreateCounter(MetricName, "", Array.Empty<string>());

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
            var handle1 = _expiringMetrics.CreateCounter(MetricName, "", Array.Empty<string>());
            var handle2 = _expiringMetrics.CreateCounter(MetricName, "", Array.Empty<string>());

            Assert.AreSame(handle1, handle2);
        }

        [TestMethod]
        public void ManagedLifetimeMetric_ViaDifferentFactories_IsSameMetric()
        {
            var handle1 = _expiringMetrics.CreateCounter(MetricName, "", Array.Empty<string>());

            var expiringMetrics2 = _metrics.WithManagedLifetime(expiresAfter: TimeSpan.FromHours(24));
            var handle2 = expiringMetrics2.CreateCounter(MetricName, "", Array.Empty<string>());

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
            var handle = (ManagedLifetimeCounter)_expiringMetrics.CreateCounter(MetricName, "", Array.Empty<string>());

            // We break delays on demand to force any expiring-eligible metrics to expire.
            var delayer = new BreakableDelayer();
            handle.Delayer = delayer;

            // We detect expiration by the value having been reset when we try allocate the counter again.

            using (handle.AcquireLease(out var instance1))
            {
                instance1.Inc();

                using (handle.AcquireLease(out var instance2))
                {
                    instance2.Inc();

                    handle.SetAllKeepaliveTimestampsToDistantPast();
                    delayer.BreakAllDelays();
                    await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and finish expiring.

                    // 2 leases remain - should not have expired yet. Check with a fresh copy from the root registry.
                    Assert.AreEqual(2, _metrics.CreateCounter(MetricName, "").Value);
                }

                handle.SetAllKeepaliveTimestampsToDistantPast();
                delayer.BreakAllDelays();
                await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and finish expiring.

                // 1 lease remains - should not have expired yet. Check with a fresh copy from the root registry.
                Assert.AreEqual(2, _metrics.CreateCounter(MetricName, "").Value);
            }

            handle.SetAllKeepaliveTimestampsToDistantPast();
            delayer.BreakAllDelays();
            await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and finish expiring.

            handle.DebugDumpLifetimes();

            // 0 leases remains - should have expired. Check with a fresh copy from the root registry.
            Assert.AreEqual(0, _metrics.CreateCounter(MetricName, "").Value);
        }

        [TestMethod]
        public async Task ManagedLifetimeMetric_WithMultipleLabelingPaths_SharedSameLifetime()
        {
            // Two calls with the same X, Y to ManagedLifetimeMetricFactory.WithLabels(X).CreateCounter().AcquireLease(Y)
            // will impact the same metric lifetime (and, implicitly, share the same metric instance data).
            // A call with matching ManagedLifetimeMetricFactory.CreateCounter(X).AcquireLease(Y) will do the same.

            var label1Key = "test_label_1";
            var label2Key = "test_label_2";
            var label1Value = "some value 1";
            var label2Value = "some value 2";
            var labels = new Dictionary<string, string>
            {
                { label1Key, label1Value },
                { label2Key, label2Value },
            };

            // Must be ordinal-sorted to match the WithLabels() sorting.
            var labelNames = new[] { label1Key, label2Key };
            var labelValues = new[] { label1Value, label2Value };

            var labelingFactory1 = _expiringMetrics.WithLabels(labels);
            var labelingFactory2 = _expiringMetrics.WithLabels(labels);

            var factory1Handle = (LabelEnrichingManagedLifetimeCounter)labelingFactory1.CreateCounter(MetricName, "", Array.Empty<string>());
            var factory2Handle = (LabelEnrichingManagedLifetimeCounter)labelingFactory2.CreateCounter(MetricName, "", Array.Empty<string>());

            var rawHandle = (ManagedLifetimeCounter)_expiringMetrics.CreateCounter(MetricName, "", labelNames);

            // We break delays on demand to force any expiring-eligible metrics to expire.
            var delayer = new BreakableDelayer();
            ((ManagedLifetimeCounter)factory1Handle._inner).Delayer = delayer;
            ((ManagedLifetimeCounter)factory2Handle._inner).Delayer = delayer;
            rawHandle.Delayer = delayer;

            // We detect expiration by the value having been reset when we try allocate the counter again.

            using (factory1Handle.AcquireLease(out var instance1))
            {
                instance1.Inc();

                using (factory2Handle.AcquireLease(out var instance2))
                {
                    instance2.Inc();

                    rawHandle.SetAllKeepaliveTimestampsToDistantPast();
                    delayer.BreakAllDelays();
                    await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and finish expiring.

                    // 2 leases remain - should not have expired yet. Check with a fresh copy from the root registry.
                    Assert.AreEqual(2, _metrics.CreateCounter(MetricName, "", labelNames).WithLabels(labelValues).Value);
                }

                rawHandle.SetAllKeepaliveTimestampsToDistantPast();
                delayer.BreakAllDelays();
                await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and finish expiring.

                // 1 lease remains - should not have expired yet. Check with a fresh copy from the root registry.
                Assert.AreEqual(2, _metrics.CreateCounter(MetricName, "", labelNames).WithLabels(labelValues).Value);

                using (rawHandle.AcquireLease(out var instance3, labelValues))
                {
                    instance3.Inc();

                    rawHandle.SetAllKeepaliveTimestampsToDistantPast();
                    delayer.BreakAllDelays();
                    await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and finish expiring.

                    // 2 leases remain - should not have expired yet. Check with a fresh copy from the root registry.
                    Assert.AreEqual(3, _metrics.CreateCounter(MetricName, "", labelNames).WithLabels(labelValues).Value);
                }

                rawHandle.SetAllKeepaliveTimestampsToDistantPast();
                delayer.BreakAllDelays();
                await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and finish expiring.

                // 1 lease remains - should not have expired yet. Check with a fresh copy from the root registry.
                Assert.AreEqual(3, _metrics.CreateCounter(MetricName, "", labelNames).WithLabels(labelValues).Value);
            }

            rawHandle.SetAllKeepaliveTimestampsToDistantPast();
            delayer.BreakAllDelays();
            await Task.Delay(WaitForAsyncActionSleepTime); // Give it a moment to wake up and finish expiring.

            rawHandle.DebugDumpLifetimes();

            // 0 leases remains - should have expired. Check with a fresh copy from the root registry.
            Assert.AreEqual(0, _metrics.CreateCounter(MetricName, "", labelNames).WithLabels(labelValues).Value);
        }
    }
}
