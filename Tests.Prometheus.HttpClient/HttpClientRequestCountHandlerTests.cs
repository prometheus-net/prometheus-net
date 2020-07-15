using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus.HttpClientMetrics;

namespace Prometheus.Tests
{
    [TestClass]
    public class HttpClientRequestCountHandlerTests
    {
        [TestMethod]
        public async Task when_request_a_url_should_inc_request_count()
        {
            //////////////////////////////////////
            // Arrange
            //////////////////////////////////////

            var counter = Metrics.CreateCounter(
                                                "httpclient_requests_received_total",
                                                "Provides the count of HttpClient requests that have been called by the client.",
                                                new CounterConfiguration
                                                {
                                                    LabelNames = HttpClientRequestLabelNames.All
                                                });


            var options = new HttpClientRequestCountOptions
            {
                Counter = counter
            };

            var httpClientRequestCountHandler =
                new HttpClientRequestCountHandler(new HttpClientHandler(), options);


            var httpClient = new HttpClient(httpClientRequestCountHandler);

            //////////////////////////////////////
            // Act
            //////////////////////////////////////

            await httpClient.GetAsync("http://www.google.com").ConfigureAwait(false);

            //////////////////////////////////////
            // Assert
            //////////////////////////////////////

            Assert.AreEqual(1, counter.WithLabels("GET", "www.google.com").Value);
        }
    }
}