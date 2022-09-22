using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus.HttpClientMetrics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus.Tests.HttpClientMetrics
{
    [TestClass]
    public class HttpClientResponseDurationHandlerTests
    {
        [TestMethod]
        public async Task OnRequest_IncrementsHistogramCountAndSum()
        {
            var registry = Metrics.NewCustomRegistry();

            var options = new HttpClientResponseDurationOptions
            {
                Registry = registry
            };

            var handler = new HttpClientResponseDurationHandler(options, HttpClientIdentity.Default);

            // As we are not using the HttpClientProvider for constructing our pipeline, we need to do this manually.
            handler.InnerHandler = new HttpClientHandler();

            var client = new HttpClient(handler);
            await client.GetAsync(ConnectivityCheck.Url);

            Assert.AreEqual(1, handler._metric.WithLabels("GET", ConnectivityCheck.Host, HttpClientIdentity.Default.Name, ConnectivityCheck.ExpectedResponseCode).Count);
            Assert.IsTrue(handler._metric.WithLabels("GET", ConnectivityCheck.Host, HttpClientIdentity.Default.Name, ConnectivityCheck.ExpectedResponseCode).Sum > 0);
        }

        [TestMethod]
        public async Task OnRequest_AwaitsResponseReadingToFinish_ThenRecordsDuration()
        {
            var registry = Metrics.NewCustomRegistry();

            var options = new HttpClientResponseDurationOptions
            {
                Registry = registry
            };

            var handler = new HttpClientResponseDurationHandler(options, HttpClientIdentity.Default);

            // Use a mock client handler so we can control when the task completes
            var mockHttpClientHandler = new MockHttpClientHandler();
            handler.InnerHandler = mockHttpClientHandler;

            var client = new HttpClient(handler);
            var requestTask = client.GetAsync(ConnectivityCheck.Url, HttpCompletionOption.ResponseHeadersRead);

            // There should be no duration metric recorded unless the task is completed.
            Assert.AreEqual(0, handler._metric.WithLabels("GET", ConnectivityCheck.Host, HttpClientIdentity.Default.Name, ConnectivityCheck.ExpectedResponseCode).Count);

            mockHttpClientHandler.Complete();

            // There should be no duration metric recorded unless the response is actually read or disposed.
            Assert.AreEqual(0, handler._metric.WithLabels("GET", ConnectivityCheck.Host, HttpClientIdentity.Default.Name, ConnectivityCheck.ExpectedResponseCode).Count);

            var response = await requestTask;

            await response.Content.ReadAsStringAsync();

            // Now that we have finished reading it, it should show up.
            Assert.AreEqual(1, handler._metric.WithLabels("GET", ConnectivityCheck.Host, HttpClientIdentity.Default.Name, ConnectivityCheck.ExpectedResponseCode).Count);
        }

        [TestMethod]
        public async Task OnRequest_AwaitsResponseDisposal_ThenRecordsDuration()
        {
            var registry = Metrics.NewCustomRegistry();

            var options = new HttpClientResponseDurationOptions
            {
                Registry = registry
            };

            var handler = new HttpClientResponseDurationHandler(options, HttpClientIdentity.Default);

            // Use a mock client handler so we can control when the task completes
            var mockHttpClientHandler = new MockHttpClientHandler();
            handler.InnerHandler = mockHttpClientHandler;

            var client = new HttpClient(handler);
            var requestTask = client.GetAsync(ConnectivityCheck.Url, HttpCompletionOption.ResponseHeadersRead);

            // There should be no duration metric recorded unless the task is completed.
            Assert.AreEqual(0, handler._metric.WithLabels("GET", ConnectivityCheck.Host, HttpClientIdentity.Default.Name, ConnectivityCheck.ExpectedResponseCode).Count);

            mockHttpClientHandler.Complete();

            // There should be no duration metric recorded unless the response is actually read or disposed.
            Assert.AreEqual(0, handler._metric.WithLabels("GET", ConnectivityCheck.Host, HttpClientIdentity.Default.Name, ConnectivityCheck.ExpectedResponseCode).Count);

            var response = await requestTask;

            response.Dispose();

            // Now that we have disposed it, it should show up.
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
                _taskCompletionSource.SetResult(new HttpResponseMessage
                {
                    Content = new StringContent("test content")
                });
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return _taskCompletionSource.Task;
            }
        }
    }
}