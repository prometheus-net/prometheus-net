using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus;
using Prometheus.HttpMetrics;
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

        private CollectorRegistry _registry;
        private MetricFactory _factory;

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
            var histogram = _factory.CreateHistogram("invalid_labels_histogram", "", new HistogramConfiguration
            {
                Buckets = new[] { 1d, 2d, 3d },
                LabelNames = new[] { "invalid" }
            });

            Assert.ThrowsException<ArgumentException>(() =>
                new HttpRequestDurationMiddleware(_requestDelegate, histogram));
        }

        [TestMethod]
        public async Task Given_request_populates_labels_correctly()
        {
            var histogram = _factory.CreateHistogram("all_labels_histogram", "", new HistogramConfiguration
            {
                Buckets = new[] { 1d, 2d, 3d },
                LabelNames = HttpRequestLabelNames.All
            });

            var expectedStatusCode = 400;
            var expectedMethod = "METHOD";
            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER";
            SetupHttpContext(_httpContext, expectedStatusCode, expectedMethod, expectedAction, expectedController);
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            await _sut.Invoke(_httpContext);

            var labels = histogram.GetAllLabels().Single();
            Assert.AreEqual(expectedStatusCode.ToString(), GetLabelValueOrDefault(labels, HttpRequestLabelNames.Code));
            Assert.AreEqual(expectedMethod, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Method));
            Assert.AreEqual(expectedAction, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Action));
            Assert.AreEqual(expectedController, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Controller));
        }

        [TestMethod]
        public async Task Given_request_populates_labels_supplied_out_of_order_correctly()
        {
            var histogram = _factory.CreateHistogram("all_labels_histogram", "", new HistogramConfiguration
            {
                Buckets = new[] { 1d, 2d, 3d },
                LabelNames = HttpRequestLabelNames.All.Reverse().ToArray()
            });

            var expectedStatusCode = 400;
            var expectedMethod = "METHOD";
            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER";
            SetupHttpContext(_httpContext, expectedStatusCode, expectedMethod, expectedAction, expectedController);
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            await _sut.Invoke(_httpContext);

            var labels = histogram.GetAllLabels().Single();
            Assert.AreEqual(expectedStatusCode.ToString(), GetLabelValueOrDefault(labels, HttpRequestLabelNames.Code));
            Assert.AreEqual(expectedMethod, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Method));
            Assert.AreEqual(expectedAction, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Action));
            Assert.AreEqual(expectedController, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Controller));
        }

        [TestMethod]
        public async Task Given_request_populates_subset_of_labels_correctly()
        {
            var histogram = _factory.CreateHistogram("all_labels_histogram", "", new HistogramConfiguration
            {
                Buckets = new[] { 1d, 2d, 3d },
                LabelNames = new[] { HttpRequestLabelNames.Action, HttpRequestLabelNames.Controller }
            });

            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER";
            SetupHttpContext(_httpContext, 200, "method", expectedAction, expectedController);
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            await _sut.Invoke(_httpContext);

            var labels = histogram.GetAllLabels().Single();
            Assert.IsNull(GetLabelValueOrDefault(labels, HttpRequestLabelNames.Code));
            Assert.IsNull(GetLabelValueOrDefault(labels, HttpRequestLabelNames.Method));
            Assert.AreEqual(expectedAction, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Action));
            Assert.AreEqual(expectedController, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Controller));
        }

        [TestMethod]
        public async Task Given_multiple_requests_populates_code_label_correctly()
        {
            var histogram = _factory.CreateHistogram("histogram", "", new HistogramConfiguration
            {
                Buckets = new[] { 0.1d, 1d, 10d },
                LabelNames = new[] { HttpRequestLabelNames.Code }
            });
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            var expectedStatusCode1 = await SetStatusCodeAndInvoke(400);
            var expectedStatusCode2 = await SetStatusCodeAndInvoke(401);

            var labels = histogram.GetAllLabels();
            var codes = GetLabelValues(labels, HttpRequestLabelNames.Code);

            Assert.AreEqual(2, codes.Length);
            CollectionAssert.AreEquivalent(new[] { expectedStatusCode1.ToString(), expectedStatusCode2.ToString() }, codes);
        }

        [TestMethod]
        public async Task Given_multiple_requests_populates_method_label_correctly()
        {
            var histogram = _factory.CreateHistogram("histogram", "", new HistogramConfiguration
            {
                Buckets = new[] { 0.1d, 1d, 10d },
                LabelNames = new[] { HttpRequestLabelNames.Method }
            });
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            var expectedMethod1 = await SetMethodAndInvoke("POST");
            var expectedMethod2 = await SetMethodAndInvoke("GET");

            var labels = histogram.GetAllLabels();
            var methods = GetLabelValues(labels, HttpRequestLabelNames.Method);

            Assert.AreEqual(2, methods.Length);
            CollectionAssert.AreEquivalent(new[] { expectedMethod1, expectedMethod2 }, methods);
        }

        [TestMethod]
        public async Task Given_multiple_requests_populates_controller_label_correctly()
        {
            var histogram = _factory.CreateHistogram("histogram", "", new HistogramConfiguration
            {
                Buckets = new[] { 0.1d, 1d, 10d },
                LabelNames = new[] { HttpRequestLabelNames.Controller }
            });
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            var expectedController1 = await SetControllerAndInvoke("ValuesController");
            var expectedController2 = await SetControllerAndInvoke("AuthController");

            var labels = histogram.GetAllLabels();
            var controllers = GetLabelValues(labels, HttpRequestLabelNames.Controller);

            Assert.AreEqual(2, controllers.Length);
            CollectionAssert.AreEquivalent(new[] { expectedController1, expectedController2 }, controllers);
        }

        [TestMethod]
        public async Task Given_multiple_requests_populates_action_label_correctly()
        {
            var histogram = _factory.CreateHistogram("histogram", "", new HistogramConfiguration
            {
                Buckets = new[] { 0.1d, 1d, 10d },
                LabelNames = new[] { HttpRequestLabelNames.Action }
            });
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            var expectedAction1 = await SetActionAndInvoke("Action1");
            var expectedAction2 = await SetActionAndInvoke("Action2");

            var labels = histogram.GetAllLabels();
            var actions = GetLabelValues(labels, HttpRequestLabelNames.Action);

            Assert.AreEqual(2, actions.Length);
            CollectionAssert.AreEquivalent(new[] { expectedAction1, expectedAction2 }, actions);
        }

        [TestInitialize]
        public void Init()
        {
            _registry = Metrics.NewCustomRegistry();
            _factory = Metrics.WithCustomRegistry(_registry);

            _histogram = _factory.CreateHistogram("default_histogram", "", new HistogramConfiguration
            {
                Buckets = new[] { 0.1d, 1d, 10d }
            });
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