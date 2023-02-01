using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus.Tests
{
    [TestClass]
    public sealed class MetricPusherTests
    {
        [TestMethod]
        public void OnError_CallsErrorCallback()
        {
            Exception lastError = null;
            var onErrorCalled = new ManualResetEventSlim();

            void OnError(Exception ex)
            {
                lastError = ex;
                onErrorCalled.Set();
            }

            var pusher = new MetricPusher(new MetricPusherOptions
            {
                Job = "Test",
                // Small interval to ensure that we exit fast.
                IntervalMilliseconds = 100,
                // Nothing listening there, should throw error right away.
                Endpoint = "https://127.0.0.1:0",
                OnError = OnError
            });

            pusher.Start();

            var onErrorWasCalled = onErrorCalled.Wait(TimeSpan.FromSeconds(10));
            Assert.IsTrue(onErrorWasCalled, "OnError was not called even though at least one failed push should have happened already.");
            Assert.IsNotNull(lastError);

            pusher.Stop();
        }

        [TestMethod]
        public void HttpClient_OnInvalidRequestUri_CallsErrorCallback()
        {
            RunHttpClientExceptionScenario(new InvalidOperationException("Simulating an invalid request uri according to HttpClient documentation."));
        }

        [TestMethod]
        public void HttpClient_OnUnderlyingConnectionIssue_CallsErrorCallback()
        {
            RunHttpClientExceptionScenario(new HttpRequestException("Simulating an underlying connection issue according to HttpClient documentation."));
        }

        [TestMethod]
        public void HttpClient_OnTimeout_CallsErrorCallback()
        {
            RunHttpClientExceptionScenario(new TaskCanceledException("Simulating a timeout according to HttpClient documentation."));
        }

        private void RunHttpClientExceptionScenario(Exception throwOnHttpPost)
        {
            Exception lastError = null;
            var onErrorCalled = new ManualResetEventSlim();

            void OnError(Exception ex)
            {
                lastError = ex;
                onErrorCalled.Set();
            }

            var pusher = new MetricPusher(new MetricPusherOptions
            {
                Job = "Test",
                // Small interval to ensure that we exit fast.
                IntervalMilliseconds = 100,
                Endpoint = "https://any_valid.url/the_push_fails_with_fake_httpclient_throwing_exceptions",
                OnError = OnError,
                HttpClientProvider = () => new ThrowingHttpClient(throwOnHttpPost)
            });

            pusher.Start();

            var onErrorWasCalled = onErrorCalled.Wait(TimeSpan.FromSeconds(5));
            Assert.IsTrue(onErrorWasCalled, "OnError was not called even though the push failed.");
            Assert.IsNotNull(lastError);

            pusher.Stop();
        }

        private class ThrowingHttpClient : HttpClient
        {
            private readonly Exception _throwOnSend;
            public ThrowingHttpClient(Exception ex)
            {
                _throwOnSend = ex;
            }

            // PostAsync eventually calls SendAsync
            public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                throw _throwOnSend;
            }
        }
    }
}
