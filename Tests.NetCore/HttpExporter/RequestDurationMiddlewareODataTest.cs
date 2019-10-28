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
    public class RequestDurationMiddlewareODataTest
    {
        private Histogram _histogram;
        private DefaultHttpContext _httpContext;
        private RequestDelegate _requestDelegate;

        private CollectorRegistry _registry;
        private MetricFactory _factory;

        private HttpRequestDurationMiddleware _sut;


        [DataTestMethod]
        [DataRow("'X'")]
        [DataRow("1")]
        public async Task Given_request_populates_labels_correctly(string key)
        {
            var histogram = _factory.CreateHistogram("all_labels_histogram", "", new HistogramConfiguration
            {
                Buckets = new[] { 1d, 2d, 3d },
                LabelNames = HttpRequestLabelNames.All
            });

            var expectedStatusCode = 400;
            var expectedMethod = "METHOD";
            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER()";
            SetupHttpContextOData(_httpContext, expectedStatusCode, expectedMethod, expectedAction, "CONTROLLER", key);
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            await _sut.Invoke(_httpContext);

            var labels = histogram.GetAllLabels().Single();
            Assert.AreEqual(expectedStatusCode.ToString(), GetLabelValueOrDefault(labels, HttpRequestLabelNames.Code));
            Assert.AreEqual(expectedMethod, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Method));
            Assert.AreEqual(expectedAction, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Action));
            Assert.AreEqual(expectedController, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Controller));
        }

        [DataTestMethod]
        [DataRow("'X'")]
        [DataRow("1")]
        public async Task Given_request_populates_labels_supplied_out_of_order_correctly(string key)
        {
            var histogram = _factory.CreateHistogram("all_labels_histogram", "", new HistogramConfiguration
            {
                Buckets = new[] { 1d, 2d, 3d },
                LabelNames = HttpRequestLabelNames.All.Reverse().ToArray()
            });

            var expectedStatusCode = 400;
            var expectedMethod = "METHOD";
            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER()";
            SetupHttpContextOData(_httpContext, expectedStatusCode, expectedMethod, expectedAction, "CONTROLLER", key);
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            await _sut.Invoke(_httpContext);

            var labels = histogram.GetAllLabels().Single();
            Assert.AreEqual(expectedStatusCode.ToString(), GetLabelValueOrDefault(labels, HttpRequestLabelNames.Code));
            Assert.AreEqual(expectedMethod, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Method));
            Assert.AreEqual(expectedAction, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Action));
            Assert.AreEqual(expectedController, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Controller));
        }

        [DataTestMethod]
        [DataRow("'X'")]
        [DataRow("1")]
        public async Task Given_request_populates_subset_of_labels_correctly(string key)
        {
            var histogram = _factory.CreateHistogram("all_labels_histogram", "", new HistogramConfiguration
            {
                Buckets = new[] { 1d, 2d, 3d },
                LabelNames = new[] { HttpRequestLabelNames.Action, HttpRequestLabelNames.Controller }
            });

            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER()";
            SetupHttpContextOData(_httpContext, 200, "method", expectedAction, "CONTROLLER", key);
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            await _sut.Invoke(_httpContext);

            var labels = histogram.GetAllLabels().Single();
            Assert.IsNull(GetLabelValueOrDefault(labels, HttpRequestLabelNames.Code));
            Assert.IsNull(GetLabelValueOrDefault(labels, HttpRequestLabelNames.Method));
            Assert.AreEqual(expectedAction, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Action));
            Assert.AreEqual(expectedController, GetLabelValueOrDefault(labels, HttpRequestLabelNames.Controller));
        }


        [DataTestMethod]
        [DataRow("'X'")]
        [DataRow("1")]
        public async Task Given_multiple_requests_populates_controller_label_correctly(string key)
        {
            var histogram = _factory.CreateHistogram("histogram", "", new HistogramConfiguration
            {
                Buckets = new[] { 0.1d, 1d, 10d },
                LabelNames = new[] { HttpRequestLabelNames.Controller }
            });
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            var expectedController1 = await SetControllerAndInvoke("ValuesController", key);
            var expectedController2 = await SetControllerAndInvoke("AuthController", key);

            var labels = histogram.GetAllLabels();
            var controllers = GetLabelValues(labels, HttpRequestLabelNames.Controller);

            Assert.AreEqual(2, controllers.Length);
            CollectionAssert.AreEquivalent(new[] { expectedController1, expectedController2 }, controllers);
        }

        [TestMethod]
        public async Task Given_multiple_requests_populates_controller_label_correctly_parameter()
        {
            var histogram = _factory.CreateHistogram("histogram", "", new HistogramConfiguration
            {
                Buckets = new[] { 0.1d, 1d, 10d },
                LabelNames = new[] { HttpRequestLabelNames.Controller }
            });
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            await SetControllerAndInvoke(new RouteData
            {
                Values = { { "odataPath", "AuthController(ItemKey='X')" }, { "ItemKey", "X" } }
            });

            var labels = histogram.GetAllLabels();
            var controllers = GetLabelValues(labels, HttpRequestLabelNames.Controller);
            
            CollectionAssert.AreEquivalent(new[] { "AuthController(ItemKey=)" }, controllers);
        }

        [TestMethod]
        public async Task Given_multiple_requests_populates_controller_label_correctly_parameter_number()
        {
            var histogram = _factory.CreateHistogram("histogram", "", new HistogramConfiguration
            {
                Buckets = new[] { 0.1d, 1d, 10d },
                LabelNames = new[] { HttpRequestLabelNames.Controller }
            });
            _sut = new HttpRequestDurationMiddleware(_requestDelegate, histogram);

            await SetControllerAndInvoke(new RouteData
            {
                Values = {{"odataPath", "AuthController(Number=1)"}, {"Number", "1"}}
            });
            
            var labels = histogram.GetAllLabels();
            var controllers = GetLabelValues(labels, HttpRequestLabelNames.Controller);
            
            CollectionAssert.AreEquivalent(new[] { "AuthController(Number=)" }, controllers);
        }

        [TestMethod]
        public async Task Given_multiple_requests_populates_controller_label_correctly_no_key()
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

        private async Task SetControllerAndInvoke(RouteData routeData)
        {
            _httpContext.Features[typeof(IRoutingFeature)] = new FakeRoutingFeature
            {
                RouteData = routeData
            };
            await _sut.Invoke(_httpContext);
        }
    }
}