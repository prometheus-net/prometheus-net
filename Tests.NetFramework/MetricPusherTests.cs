using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

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
                Endpoint = "https://127.0.0.1:1",
                OnError = OnError
            });

            pusher.Start();

            var onErrorWasCalled = onErrorCalled.Wait(TimeSpan.FromSeconds(5));
            Assert.IsTrue(onErrorWasCalled, "OnError was not called even though at least one failed push should have happened already.");
            Assert.IsNotNull(lastError);

            pusher.Stop();
        }
    }
}
