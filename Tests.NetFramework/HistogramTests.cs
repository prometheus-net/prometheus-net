using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Prometheus.Tests
{
    [TestClass]
    public sealed class HistogramTests
    {
        [TestMethod]
        public void Observe_IncrementsCorrectBucketsAndCountAndSum()
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
            histogram.CollectAndSerialize(serializer);

            // Sum
            // Count
            // 1.0
            // 2.0
            // 3.0
            // +inf
            serializer.Received().WriteMetric(histogram.Unlabelled._sumIdentifier, 5.0);
            serializer.Received().WriteMetric(histogram.Unlabelled._countIdentifier, 2.0);
            serializer.Received().WriteMetric(histogram.Unlabelled._bucketIdentifiers[0], 0);
            serializer.Received().WriteMetric(histogram.Unlabelled._bucketIdentifiers[1], 1);
            serializer.Received().WriteMetric(histogram.Unlabelled._bucketIdentifiers[2], 2);
            serializer.Received().WriteMetric(histogram.Unlabelled._bucketIdentifiers[3], 2);
        }
    }
}
