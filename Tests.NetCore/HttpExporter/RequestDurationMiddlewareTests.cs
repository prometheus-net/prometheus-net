using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus;
using Prometheus.Advanced;
using Prometheus.AspNetCore.HttpExporter;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Tests.HttpExporter.MetricTestHelpers;

namespace Tests.HttpExporter
{
    [TestClass]
    public class RequestDurationMiddlewareTests
    {
        private Histogram _histogram;
        private DefaultHttpContext _httpContext;
        private RequestDelegate _requestDelegate;

        private HttpRequestDurationMiddleware _sut;

        [TestMethod]
        public void Given_null_histogram_then_throws()
        {
            Assert.ThrowsException<ArgumentException>(() =>
                new HttpRequestDurationMiddleware(_requestDelegate, null));
        }

        [TestMethod]
        public void Given_invalid_labels_then_throws()
        {
            var histogram = Metrics.CreateHistogram("invalid_labels_histogram", "", new[] { 1d, 2d, 3d }, "invalid");

            Assert.ThrowsException<ArgumentException>(() =>
                new HttpRequestDurationMiddleware(_requestDelegate, histogram));
        }

        [TestMethod]
        public async Task Given_request_populates_labels_correctly()
        {
            var histogram = Metrics.CreateHistogram("all_labels_histogram", "", new[] { 1d, 2d, 3d },
                HttpRequestLabelNames.All);

            var expectedStatusCode = 400;
            var expectedMethod = "METHOD";
            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER";
            SetupHttpContext(_httpContext, expectedStatusCode, expectedMethod, expectedAction, expectedController);
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            await _sut.Invoke(_httpContext);

            var collectedMetrics = GetCollectedMetrics(histogram);
            Assert.AreEqual(expectedStatusCode.ToString(), GetLabelData(collectedMetrics, HttpRequestLabelNames.Code));
            Assert.AreEqual(expectedMethod, GetLabelData(collectedMetrics, HttpRequestLabelNames.Method));
            Assert.AreEqual(expectedAction, GetLabelData(collectedMetrics, HttpRequestLabelNames.Action));
            Assert.AreEqual(expectedController, GetLabelData(collectedMetrics, HttpRequestLabelNames.Controller));
        }

        [TestMethod]
        public async Task Given_request_populates_labels_supplied_out_of_order_correctly()
        {
            var histogram = Metrics.CreateHistogram("all_labels_histogram", "", new[] { 1d, 2d, 3d },
                HttpRequestLabelNames.All.Reverse().ToArray());

            var expectedStatusCode = 400;
            var expectedMethod = "METHOD";
            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER";
            SetupHttpContext(_httpContext, expectedStatusCode, expectedMethod, expectedAction, expectedController);
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            await _sut.Invoke(_httpContext);

            var collectedMetrics = GetCollectedMetrics(histogram);
            Assert.AreEqual(expectedStatusCode.ToString(), GetLabelData(collectedMetrics, HttpRequestLabelNames.Code));
            Assert.AreEqual(expectedMethod, GetLabelData(collectedMetrics, HttpRequestLabelNames.Method));
            Assert.AreEqual(expectedAction, GetLabelData(collectedMetrics, HttpRequestLabelNames.Action));
            Assert.AreEqual(expectedController, GetLabelData(collectedMetrics, HttpRequestLabelNames.Controller));
        }

        [TestMethod]
        public async Task Given_request_populates_subset_of_labels_correctly()
        {
            var histogram = Metrics.CreateHistogram("all_labels_histogram", "", new[] { 1d, 2d, 3d },
                HttpRequestLabelNames.Action, HttpRequestLabelNames.Controller);

            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER";
            SetupHttpContext(_httpContext, 200, "method", expectedAction, expectedController);
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            await _sut.Invoke(_httpContext);

            var collectedMetrics = GetCollectedMetrics(histogram);
            Assert.IsNull(GetLabelData(collectedMetrics, HttpRequestLabelNames.Code));
            Assert.IsNull(GetLabelData(collectedMetrics, HttpRequestLabelNames.Method));
            Assert.AreEqual(expectedAction, GetLabelData(collectedMetrics, HttpRequestLabelNames.Action));
            Assert.AreEqual(expectedController, GetLabelData(collectedMetrics, HttpRequestLabelNames.Controller));
        }

        [TestMethod]
        public async Task Given_multiple_requests_populates_code_label_correctly()
        {
            var histogram = Metrics.CreateHistogram("histogram", "", new[] { 0.1d, 1d, 10d }, HttpRequestLabelNames.Code);
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            var expectedStatusCode1 = await SetStatusCodeAndInvoke(400);
            var expectedStatusCode2 = await SetStatusCodeAndInvoke(401);

            var collectedMetrics = GetCollectedMetrics(histogram);
            Assert.AreEqual(2, collectedMetrics.Count);
            Assert.AreEqual(1UL,
                GetLabelHistogram(collectedMetrics, HttpRequestLabelNames.Code, expectedStatusCode1).sample_count);
            Assert.AreEqual(1UL,
                GetLabelHistogram(collectedMetrics, HttpRequestLabelNames.Code, expectedStatusCode2).sample_count);
        }

        [TestMethod]
        public async Task Given_multiple_requests_populates_method_label_correctly()
        {
            var histogram =
                Metrics.CreateHistogram("histogram", "", new[] { 0.1d, 1d, 10d }, HttpRequestLabelNames.Method);
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            var expectedMethod1 = await SetMethodAndInvoke("POST");
            var expectedMethod2 = await SetMethodAndInvoke("GET");

            var collectedMetrics = GetCollectedMetrics(histogram);
            Assert.AreEqual(2, collectedMetrics.Count);
            Assert.AreEqual(1UL,
                GetLabelHistogram(collectedMetrics, HttpRequestLabelNames.Method, expectedMethod1).sample_count);
            Assert.AreEqual(1UL,
                GetLabelHistogram(collectedMetrics, HttpRequestLabelNames.Method, expectedMethod2).sample_count);
        }

        [TestMethod]
        public async Task Given_multiple_requests_populates_controller_label_correctly()
        {
            var histogram =
                Metrics.CreateHistogram("histogram", "", new[] { 0.1d, 1d, 10d }, HttpRequestLabelNames.Controller);
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            var expectedController1 = await SetControllerAndInvoke("ValuesController");
            var expectedController2 = await SetControllerAndInvoke("AuthController");

            var collectedMetrics = GetCollectedMetrics(histogram);
            Assert.AreEqual(2, collectedMetrics.Count);
            Assert.AreEqual(1UL,
                GetLabelHistogram(collectedMetrics, HttpRequestLabelNames.Controller, expectedController1)
                    .sample_count);
            Assert.AreEqual(1UL,
                GetLabelHistogram(collectedMetrics, HttpRequestLabelNames.Controller, expectedController2)
                    .sample_count);
        }

        [TestMethod]
        public async Task Given_multiple_requests_populates_action_label_correctly()
        {
            var histogram =
                Metrics.CreateHistogram("histogram", "", new[] { 0.1d, 1d, 10d }, HttpRequestLabelNames.Action);
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            var expectedAction1 = await SetActionAndInvoke("Action1");
            var expectedAction2 = await SetActionAndInvoke("Action2");

            var collectedMetrics = GetCollectedMetrics(histogram);
            Assert.AreEqual(2, collectedMetrics.Count);
            Assert.AreEqual(1UL,
                GetLabelHistogram(collectedMetrics, HttpRequestLabelNames.Action, expectedAction1).sample_count);
            Assert.AreEqual(1UL,
                GetLabelHistogram(collectedMetrics, HttpRequestLabelNames.Action, expectedAction2).sample_count);
        }

        [TestMethod]
        public async Task Given_request_duration_populates_appropriate_bin()
        {
            var histogram = Metrics.CreateHistogram("all_labels_histogram", "", new[] { 0.01d, 0.1d, 1d },
                HttpRequestLabelNames.Code);

            await SetRequestDurationAndInvoke(histogram, TimeSpan.Zero);
            await SetRequestDurationAndInvoke(histogram, TimeSpan.FromSeconds(0.02));
            await SetRequestDurationAndInvoke(histogram, TimeSpan.FromSeconds(0.2));

            var collectedMetrics = GetCollectedMetrics(histogram);
            var resultHistogram = GetLabelHistogram(collectedMetrics, HttpRequestLabelNames.Code, 200);
            Assert.AreEqual(3UL, resultHistogram.sample_count);
            Assert.AreEqual(1UL, resultHistogram.bucket[0].cumulative_count);
            Assert.AreEqual(2UL, resultHistogram.bucket[1].cumulative_count);
            Assert.AreEqual(3UL, resultHistogram.bucket[2].cumulative_count);
        }

        [TestInitialize]
        public void Init()
        {
            DefaultCollectorRegistry.Instance.Clear();
            _histogram = Metrics.CreateHistogram("default_histogram", "", new[] { 0.1d, 1d, 10d });
            _requestDelegate = context => Task.CompletedTask;
            _httpContext = new DefaultHttpContext();
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, _histogram);
        }

        private async Task<int> SetStatusCodeAndInvoke(int expectedStatusCode)
        {
            _httpContext.Response.StatusCode = expectedStatusCode;
            await _sut.Invoke(_httpContext);
            return expectedStatusCode;
        }

        private async Task<string> SetMethodAndInvoke(string expectedMethod)
        {
            _httpContext.Request.Method = expectedMethod;
            await _sut.Invoke(_httpContext);
            return expectedMethod;
        }

        private async Task<string> SetControllerAndInvoke(string expectedController)
        {
            _httpContext.Features[typeof(IRoutingFeature)] = new FakeRoutingFeature
            {
                RouteData = new RouteData
                {
                    Values = { { "Controller", expectedController } }
                }
            };
            await _sut.Invoke(_httpContext);
            return expectedController;
        }

        private async Task<string> SetActionAndInvoke(string expectedAction)
        {
            _httpContext.Features[typeof(IRoutingFeature)] = new FakeRoutingFeature
            {
                RouteData = new RouteData
                {
                    Values = { { "Action", expectedAction } }
                }
            };
            await _sut.Invoke(_httpContext);
            return expectedAction;
        }

        private Task SetRequestDurationAndInvoke(Histogram histogram, TimeSpan duration)
        {
            _requestDelegate = context =>
            {
                Thread.Sleep(duration);
                return Task.CompletedTask;
            };
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);
            return _sut.Invoke(_httpContext);
        }
    }
}