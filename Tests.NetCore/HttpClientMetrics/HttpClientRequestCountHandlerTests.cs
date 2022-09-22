using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus.HttpClientMetrics;
using System.Net.Http;
using System.Threading.Tasks;

namespace Prometheus.Tests.HttpClientMetrics
{
    [TestClass]
    public class HttpClientRequestCountHandlerTests
    {
        [TestMethod]
        public async Task OnRequest_IncrementRequestCount()
        {
            var registry = Metrics.NewCustomRegistry();

            var options = new HttpClientRequestCountOptions
            {
                Registry = registry
            };

            var handler = new HttpClientRequestCountHandler(options, HttpClientIdentity.Default);

            // As we are not using the HttpClientProvider for constructing our pipeline, we need to do this manually.
            handler.InnerHandler = new HttpClientHandler();

            var client = new HttpClient(handler);
            await client.GetAsync(ConnectivityCheck.Url);

            Assert.AreEqual(1, handler._metric.WithLabels("GET", ConnectivityCheck.Host, HttpClientIdentity.Default.Name, ConnectivityCheck.ExpectedResponseCode).Value);
        }
    }
}