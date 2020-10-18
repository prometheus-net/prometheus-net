using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus.HttpClientMetrics;

namespace Prometheus.Tests.HttpClientMetrics
{
    [TestClass]
    public class HttpClientRequestDurationHandlerTests
    {
        [TestMethod]
        public async Task when_request_a_url_should_inc_histogram_count_the_sum()
        {
            //////////////////////////////////////
            // Arrange
            //////////////////////////////////////

            var histogram = Metrics.CreateHistogram(
                                                    "httpclient_request_duration_seconds",
                                                    "The duration of HTTP requests processed by a HttpClient.",
                                                    new HistogramConfiguration
                                                    {
                                                        // 1 ms to 32K ms buckets
                                                        Buckets = Histogram.ExponentialBuckets(0.001, 2, 16),
                                                        LabelNames = HttpClientRequestLabelNames.All
                                                    });


            var options = new HttpClientRequestDurationOptions
            {
                Histogram = histogram
            };

            var httpClientRequestDurationHandler =
                new HttpClientRequestDurationHandler(new HttpClientHandler(), options);


            var httpClient = new HttpClient(httpClientRequestDurationHandler);

            //////////////////////////////////////
            // Act
            //////////////////////////////////////

            await httpClient.GetAsync("http://www.google.com").ConfigureAwait(false);

            //////////////////////////////////////
            // Assert
            //////////////////////////////////////

            Assert.AreEqual(1, histogram.WithLabels("GET", "www.google.com").Count);
            Assert.IsTrue(histogram.WithLabels("GET", "www.google.com").Sum > 0);
        }
    }
}