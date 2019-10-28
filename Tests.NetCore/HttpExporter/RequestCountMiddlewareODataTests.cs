using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus;
using Prometheus.HttpMetrics;
using System.Linq;
using System.Threading.Tasks;
using static Tests.HttpExporter.MetricTestHelpers;

namespace Tests.HttpExporter
{
    [TestClass]
    public class RequestCountMiddlewareODataTests
    {
        private Counter _counter;
        private DefaultHttpContext _httpContext;
        private RequestDelegate _requestDelegate;

        private CollectorRegistry _registry;
        private MetricFactory _factory;

        private HttpRequestCountMiddleware _sut;

        [DataTestMethod]
        [DataRow("'X'")]
        [DataRow("1")]
        public async Task Given_request_populates_labels_correctly(string key)
        {
            var counter = _factory.CreateCounter("all_labels_counter", "", HttpRequestLabelNames.All);

            var expectedStatusCode = 400;
            var expectedMethod = "METHOD";
            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER()";
            SetupHttpContextOData(_httpContext, expectedStatusCode, expectedMethod, expectedAction, "CONTROLLER", key);
            _sut = new HttpRequestCountMiddleware(_requestDelegate, counter);

            await _sut.Invoke(_httpContext);

            var labels = counter.GetAllLabels().Single();
            Assert.AreEqual(expectedStatusCode.ToString(), GetLabelValueOrDefault(labels, HttpRequestLabelNames.Code));
            Assert.AreEqual(expectedMethod, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Method));
            Assert.AreEqual(expectedAction, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Action));
            Assert.AreEqual(expectedController, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Controller));
        }

        [DataTestMethod]
        [DataRow("'X'")]
        [DataRow("1")]
        public async Task Given_multiple_requests_populates_controller_label_correctly(string key)
        {
            var counter = _factory.CreateCounter("counter", "", HttpRequestLabelNames.Controller);
            _sut = new HttpRequestCountMiddleware(_requestDelegate, counter);

            var expectedController1 = await SetControllerAndInvoke("ValuesController", key);
            var expectedController2 = await SetControllerAndInvoke("AuthController", key);

            var labels = counter.GetAllLabels();
            var controllers = GetLabelValues(labels, HttpRequestLabelNames.Controller);

            Assert.AreEqual(2, controllers.Length);
            CollectionAssert.AreEquivalent(new[] { expectedController1, expectedController2 }, controllers);
        }

        [TestMethod]
        public async Task Given_multiple_requests_populates_controller_label_correctly_no_key()
        {
            var counter = _factory.CreateCounter("counter", "", HttpRequestLabelNames.Controller);
            _sut = new HttpRequestCountMiddleware(_requestDelegate, counter);

            var expectedController1 = await SetControllerAndInvoke("ValuesController()");
            var expectedController2 = await SetControllerAndInvoke("AuthController()");

            var labels = counter.GetAllLabels();
            var controllers = GetLabelValues(labels, HttpRequestLabelNames.Controller);

            Assert.AreEqual(2, controllers.Length);
            CollectionAssert.AreEquivalent(new[] { expectedController1, expectedController2 }, controllers);
        }

        [DataTestMethod]
        [DataRow("'X'")]
        [DataRow("1")]
        public async Task Given_request_populates_subset_of_labels_correctly(string key)
        {
            var counter = _factory.CreateCounter("all_labels_counter", "", HttpRequestLabelNames.Action,
                HttpRequestLabelNames.Controller);

            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER()";
            SetupHttpContextOData(_httpContext, 200, "method", expectedAction, "CONTROLLER", key);
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

        private async Task<string> SetControllerAndInvoke(string expectedController, string key)
        {
            _httpContext.Features[typeof(IRoutingFeature)] = new FakeRoutingFeature
            {
                RouteData = new RouteData
                {
                    Values = { { "odataPath", $"{expectedController}({key})" }, { "Key", key } }
                }
            };
            await _sut.Invoke(_httpContext);
            return $"{expectedController}()";
        }

        private async Task<string> SetControllerAndInvoke(string expectedController)
        {
            _httpContext.Features[typeof(IRoutingFeature)] = new FakeRoutingFeature
            {
                RouteData = new RouteData
                {
                    Values = { { "odataPath", expectedController } }
                }
            };
            await _sut.Invoke(_httpContext);
            return expectedController;
        }
    }
}