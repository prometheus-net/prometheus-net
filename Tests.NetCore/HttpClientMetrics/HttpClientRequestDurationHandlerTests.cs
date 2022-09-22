using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus.HttpClientMetrics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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

            var handler = new HttpClientRequestDurationHandler(options, HttpClientIdentity.Default);

            // As we are not using the HttpClientProvider for constructing our pipeline, we need to do this manually.
            handler.InnerHandler = new HttpClientHandler();

            var client = new HttpClient(handler);
            await client.GetAsync(ConnectivityCheck.Url);

            Assert.AreEqual(1, handler._metric.WithLabels("GET", ConnectivityCheck.Host, HttpClientIdentity.Default.Name, ConnectivityCheck.ExpectedResponseCode).Count);
            Assert.IsTrue(handler._metric.WithLabels("GET", ConnectivityCheck.Host, HttpClientIdentity.Default.Name, ConnectivityCheck.ExpectedResponseCode).Sum > 0);
        }

        [TestMethod]
        public void OnRequest_AwaitsRequestAndRecordsDuration()
        {
            var registry = Metrics.NewCustomRegistry();

            var options = new HttpClientRequestDurationOptions
            {
                Registry = registry
            };

            var handler = new HttpClientRequestDurationHandler(options, HttpClientIdentity.Default);

            // Use a mock client handler so we can control when the task completes
            var mockHttpClientHandler = new MockHttpClientHandler();
            handler.InnerHandler = mockHttpClientHandler;

            var client = new HttpClient(handler);
            client.GetAsync(ConnectivityCheck.Url);

            // There should be no duration metric recorded unless the task is completed
            Assert.AreEqual(0, handler._metric.WithLabels("GET", ConnectivityCheck.Host, HttpClientIdentity.Default.Name, ConnectivityCheck.ExpectedResponseCode).Count);

            mockHttpClientHandler.Complete();
            Assert.AreEqual(1, handler._metric.WithLabels("GET", ConnectivityCheck.Host, HttpClientIdentity.Default.Name, ConnectivityCheck.ExpectedResponseCode).Count);
        }

        private class MockHttpClientHandler : HttpClientHandler
        {
            private readonly TaskCompletionSource<HttpResponseMessage> _taskCompletionSource;

            public MockHttpClientHandler()
            {
                _taskCompletionSource = new TaskCompletionSource<HttpResponseMessage>();
            }

            public void Complete()
            {
                _taskCompletionSource.SetResult(new HttpResponseMessage());
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return _taskCompletionSource.Task;
            }
        }
    }
}