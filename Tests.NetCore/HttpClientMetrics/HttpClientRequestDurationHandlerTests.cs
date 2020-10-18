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
        public async Task OnRequest_IncrementsHistogramCountAndSum()
        {
            var registry = Metrics.NewCustomRegistry();

            var options = new HttpClientRequestDurationOptions
            {
                Registry = registry
            };

            var handler = new HttpClientRequestDurationHandler(options);

            // As we are not using the HttpClientProvider for constructing our pipeline, we need to do this manually.
            handler.InnerHandler = new HttpClientHandler();

            var client = new HttpClient(handler);
            await client.GetAsync("http://www.google.com");

            Assert.AreEqual(1, handler._metric.WithLabels("GET", "www.google.com").Count);
            Assert.IsTrue(handler._metric.WithLabels("GET", "www.google.com").Sum > 0);
        }
    }
}