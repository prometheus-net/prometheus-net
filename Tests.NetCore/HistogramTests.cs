using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Threading.Tasks;

namespace Prometheus.Tests
{
    [TestClass]
    public sealed class HistogramTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ObserveExemplarDuplicateKeys()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var histogram = factory.CreateHistogram("xxx", "");
            histogram.Observe(1, Exemplar.From(Exemplar.Pair("traceID", "123"), Exemplar.Pair("traceID", "1")));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ObserveExemplarTooManyRunes()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var key1 = "0123456789" + "0123456789" + "0123456789" + "0123456789" + "0123456789"; // 50
            var key2 = "0123456789" + "0123456789" + "0123456789" + "0123456789" + "0123456780"; // 50
            var val1 = "01234567890123"; // 14
            var val2 = "012345678901234"; // 15 (= 129)

            var histogram = factory.CreateHistogram("xxx", "");
            histogram.Observe(1, Exemplar.From(Exemplar.Pair(key1, val1), Exemplar.Pair(key2, val2)));
        }

        [TestMethod]
        public async Task ObserveExemplar_OnlyAddsExemplarToSingleBucket()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var histogram = factory.CreateHistogram("xxx", "", new HistogramConfiguration
            {
                Buckets = new[] { 1.0, 2.0, 3.0 }
            });

            var canary = "my_value_354867398";
            var exemplar = Exemplar.From(Exemplar.Pair("my_key", canary));

            // We expect the exemplar to be added to the specific bucket that the value falls into, not every bucket that gets incremented.
            // In this case, it would be the 2.0 bucket that the exemplar belongs to (the lowest-valued bucket that gets incremented).
            // OpenMetrics says "Exemplars SHOULD be put into the bucket with the highest value." but that seems backwards - it would mean
            // that every exemplar goes into the +Inf bucket, as that is always the highest value of an incremented bucket.
            histogram.Observe(1.9, exemplar);

            var serialized = await registry.CollectAndSerializeToStringAsync(ExpositionFormat.OpenMetricsText);
            
            // We expect to see it there.
            StringAssert.Contains(serialized, canary);

            // And we expect to see it only once.
            var firstIndex = serialized.IndexOf(canary);
            var lastIndex = serialized.LastIndexOf(canary);
            Assert.AreEqual(firstIndex, lastIndex);

            // And we expect to see it on the correct line.
            var expectedLine = $@"xxx_bucket{{le=""2.0""}} 1 # {{my_key=""{canary}""}}";
            StringAssert.Contains(serialized, expectedLine);
        }

        [TestMethod]
        public async Task Observe_IncrementsCorrectBucketsAndCountAndSum()
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            var histogram = factory.CreateHistogram("xxx", "", new HistogramConfiguration
            {
                Buckets = new[] { 1.0, 2.0, 3.0 }
            });

            histogram.Observe(2.0);
            histogram.Observe(3.0);

            var serializer = Substitute.For<IMetricsSerializer>();
            await histogram.CollectAndSerializeAsync(serializer, true, default);

            // Sum
            await serializer.Received().WriteMetricPointAsync(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<CanonicalLabel>(), 5.0, Arg.Any<ObservedExemplar>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>());

            // 1.0 bucket
            await serializer.Received().WriteMetricPointAsync(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<CanonicalLabel>(), 0, Arg.Any<ObservedExemplar>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>());

            // 2.0 bucket
            await serializer.Received().WriteMetricPointAsync(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<CanonicalLabel>(), 1, Arg.Any<ObservedExemplar>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>());

            // Count
            // 3.0 bucket
            // +inf bucket
            await serializer.Received(requiredNumberOfCalls: 3).WriteMetricPointAsync(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<CanonicalLabel>(), 2, Arg.Any<ObservedExemplar>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        }

        [TestMethod]
        public void PowersOfTenDividedBuckets_CreatesExpectedBuckets()
        {
            var expected = new[]
            {
                0.025, 0.050, 0.075, 0.1,
                0.25, 0.5, 0.75, 1,
                2.5, 5.0, 7.5, 10,
                25, 50, 75, 100
            };

            var actual = Histogram.PowersOfTenDividedBuckets(-2, 2, 4);

            Assert.AreEqual(expected.Length, actual.Length);

            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i]);
        }

        [TestMethod]
        public void PowersOfTenDividedBuckets_WithOverlappingEdges_CreatesExpectedBuckets()
        {
            var expected = new[]
            {
                // 10 should be in both power=1 and power=2 series
                // But we expect it only to be emitted once.
                1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
                20, 30, 40, 50, 60, 70, 80, 90, 100
            };

            var actual = Histogram.PowersOfTenDividedBuckets(0, 2, 10);

            Assert.AreEqual(expected.Length, actual.Length);

            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i]);
        }

        [TestMethod]
        public void LinearBuckets_CreatesExpectedBuckets()
        {
            var expected = new[]
            {
                0.025, 0.050, 0.075, 0.1,
                0.125, 0.150, 0.175, 0.2,
                0.225, 0.250, 0.275, 0.3,
            };

            var actual = Histogram.LinearBuckets(0.025, 0.025, 12);

            Assert.AreEqual(expected.Length, actual.Length);

            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i]);
        }

        [TestMethod]
        public void ExponentialBuckets_CreatesExpectedBuckets()
        {
            var expected = new[]
            {
                0.0078125,
                0.01171875,
                0.017578125,
                0.0263671875,
                0.03955078125,
                0.059326171875,
                0.0889892578125,
                0.13348388671875
            };

            var actual = Histogram.ExponentialBuckets(0.0078125, 1.5, 8);

            Assert.AreEqual(expected.Length, actual.Length);

            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i]);
        }

        [TestMethod]
        public void ExponentialBuckets_CreatesExpectedBuckets2()
        {
            var expected = new[]
            {
                0.0078125,
                0.0140625,
                0.0253125,
                0.0455625,
                0.0820125,
                0.1476225,
                0.2657205,
                0.4782969
            };

            var actual = Histogram.ExponentialBuckets(0.0078125, 1.8, 8);

            Assert.AreEqual(expected.Length, actual.Length);

            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i]);
        }
    }
}
