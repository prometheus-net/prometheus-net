using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus;
using Prometheus.HttpMetrics;
using System;
using System.Linq;
using System.Threading.Tasks;
using static Tests.HttpExporter.MetricTestHelpers;

namespace Tests.HttpExporter
{
    [TestClass]
    public class RequestCountMiddlewareTests
    {
        private Counter _counter;
        private DefaultHttpContext _httpContext;
        private RequestDelegate _requestDelegate;

        private CollectorRegistry _registry;
        private MetricFactory _factory;

        private HttpRequestCountMiddleware _sut;

        [TestMethod]
        public void Given_null_counter_then_throws()
        {
            Assert.ThrowsException<ArgumentException>(() =>
                new HttpRequestCountMiddleware(_requestDelegate, null));
        }

        [TestMethod]
        public void Given_invalid_labels_then_throws()
        {
            var counter = _factory.CreateCounter("invalid_labels_counter", "", "invalid");

            Assert.ThrowsException<ArgumentException>(() =>
                new HttpRequestCountMiddleware(_requestDelegate, counter));
        }

        [TestMethod]
        public async Task Given_request_then_increments_counter()
        {
            Assert.AreEqual(0, _counter.Value);

            await _sut.Invoke(new DefaultHttpContext());

            Assert.AreEqual(1, _counter.Value);
        }

        [TestMethod]
        public async Task Given_request_populates_labels_correctly()
        {
            var counter = _factory.CreateCounter("all_labels_counter", "", HttpRequestLabelNames.All);

            var expectedStatusCode = 400;
            var expectedMethod = "METHOD";
            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER";
            SetupHttpContext(_httpContext, expectedStatusCode, expectedMethod, expectedAction, expectedController);
            _sut = new HttpRequestCountMiddleware(_requestDelegate, counter);

            await _sut.Invoke(_httpContext);

            var labels = counter.GetAllLabels().Single();
            Assert.AreEqual(expectedStatusCode.ToString(), GetLabelValueOrDefault(labels, HttpRequestLabelNames.Code));
            Assert.AreEqual(expectedMethod, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Method));
            Assert.AreEqual(expectedAction, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Action));
            Assert.AreEqual(expectedController, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Controller));
        }

        [TestMethod]
        public async Task Given_multiple_requests_populates_code_label_correctly()
        {
            var counter = _factory.CreateCounter("counter", "", HttpRequestLabelNames.Code);
            _sut = new HttpRequestCountMiddleware(_requestDelegate, counter);

            var expectedStatusCode1 = await SetStatusCodeAndInvoke(400);
            var expectedStatusCode2 = await SetStatusCodeAndInvoke(401);

            var labels = counter.GetAllLabels();
            var codes = GetLabelValues(labels, HttpRequestLabelNames.Code);

            Assert.AreEqual(2, codes.Length);
            CollectionAssert.AreEquivalent(new[] { expectedStatusCode1.ToString(), expectedStatusCode2.ToString() }, codes);
        }

        [TestMethod]
        public async Task Given_multiple_requests_populates_method_label_correctly()
        {
            var counter = _factory.CreateCounter("counter", "", HttpRequestLabelNames.Method);
            _sut = new HttpRequestCountMiddleware(_requestDelegate, counter);

            var expectedMethod1 = await SetMethodAndInvoke("POST");
            var expectedMethod2 = await SetMethodAndInvoke("GET");

            var labels = counter.GetAllLabels();
            var methods = GetLabelValues(labels, HttpRequestLabelNames.Method);

            Assert.AreEqual(2, methods.Length);
            CollectionAssert.AreEquivalent(new[] { expectedMethod1, expectedMethod2 }, methods);
        }

        [TestMethod]
        public async Task Given_multiple_requests_populates_controller_label_correctly()
        {
            var counter = _factory.CreateCounter("counter", "", HttpRequestLabelNames.Controller);
            _sut = new HttpRequestCountMiddleware(_requestDelegate, counter);

            var expectedController1 = await SetControllerAndInvoke("ValuesController");
            var expectedController2 = await SetControllerAndInvoke("AuthController");

            var labels = counter.GetAllLabels();
            var controllers = GetLabelValues(labels, HttpRequestLabelNames.Controller);

            Assert.AreEqual(2, controllers.Length);
            CollectionAssert.AreEquivalent(new[] { expectedController1, expectedController2 }, controllers);
        }

        [TestMethod]
        public async Task Given_multiple_requests_populates_action_label_correctly()
        {
            var counter = _factory.CreateCounter("counter", "", HttpRequestLabelNames.Action);
            _sut = new HttpRequestCountMiddleware(_requestDelegate, counter);

            var expectedAction1 = await SetActionAndInvoke("Action1");
            var expectedAction2 = await SetActionAndInvoke("Action2");

            var labels = counter.GetAllLabels();
            var actions = GetLabelValues(labels, HttpRequestLabelNames.Action);

            Assert.AreEqual(2, actions.Length);
            CollectionAssert.AreEquivalent(new[] { expectedAction1, expectedAction2 }, actions);
        }

        [TestMethod]
        public async Task Given_request_populates_subset_of_labels_correctly()
        {
            var counter = _factory.CreateCounter("all_labels_counter", "", HttpRequestLabelNames.Action,
                HttpRequestLabelNames.Controller);

            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER";
            SetupHttpContext(_httpContext, 200, "method", expectedAction, expectedController);
            _sut = new HttpRequestCountMiddleware(_requestDelegate, counter);

            await _sut.Invoke(_httpContext);

            var labels = counter.GetAllLabels().Single();
            Assert.IsNull(GetLabelValueOrDefault(labels, HttpRequestLabelNames.Code));
            Assert.IsNull(GetLabelValueOrDefault(labels, HttpRequestLabelNames.Method));
            Assert.AreEqual(expectedAction, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Action));
            Assert.AreEqual(expectedController, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Controller));
        }

        [TestInitialize]
        public void Init()
        {
            _registry = Metrics.NewCustomRegistry();
            _factory = Metrics.WithCustomRegistry(_registry);
            _counter = _factory.CreateCounter("default_counter", "");
            _requestDelegate = context => Task.CompletedTask;

            _httpContext = new DefaultHttpContext();

            _sut = new HttpRequestCountMiddleware(_requestDelegate, _counter);
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
    }

    internal class FakeRoutingFeature : IRoutingFeature
    {
        public RouteData RouteData { get; set; }
    }
}