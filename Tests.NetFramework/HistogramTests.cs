using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Threading.Tasks;

namespace Prometheus.Tests
{
    [TestClass]
    public sealed class HistogramTests
    {
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
            await histogram.CollectAndSerializeAsync(serializer, default);

            // Sum
            // Count
            // 1.0
            // 2.0
            // 3.0
            // +inf
            await serializer.Received().WriteMetricAsync(histogram.Unlabelled._sumIdentifier, 5.0, default);
            await serializer.Received().WriteMetricAsync(histogram.Unlabelled._countIdentifier, 2.0, default);
            await serializer.Received().WriteMetricAsync(histogram.Unlabelled._bucketIdentifiers[0], 0, default);
            await serializer.Received().WriteMetricAsync(histogram.Unlabelled._bucketIdentifiers[1], 1, default);
            await serializer.Received().WriteMetricAsync(histogram.Unlabelled._bucketIdentifiers[2], 2, default);
            await serializer.Received().WriteMetricAsync(histogram.Unlabelled._bucketIdentifiers[3], 2, default);
        }
    }
}
