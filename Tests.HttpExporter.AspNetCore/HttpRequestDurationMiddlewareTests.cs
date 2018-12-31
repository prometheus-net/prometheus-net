using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus;
using Prometheus.Advanced;
using Prometheus.HttpExporter.AspNetCore.HttpRequestCount;
using Prometheus.HttpExporter.AspNetCore.HttpRequestDuration;
using Counter = Prometheus.Counter;

namespace Tests.HttpExporter.AspNetCore
{
    [TestClass]
    public class HttpRequestDurationMiddlewareTests
    {
        [TestMethod]
        public void Given_null_counter_then_throws()
        {
            Assert.ThrowsException<ArgumentException>(() =>
                new HttpRequestDurationMiddleware(this._requestDelegate, null));
        }
        
        [TestMethod]
        public void Given_invalid_labels_then_throws()
        {
            var histogram = Metrics.CreateHistogram("invalid_labels_histogram", "", new []{1d,2d,3d}, "invalid");

            Assert.ThrowsException<ArgumentException>(() =>
                new HttpRequestDurationMiddleware(this._requestDelegate, histogram));
        }
        
//        [TestMethod]
//        public async Task Given_request_then_increments_counter()
//        {
//            Assert.AreEqual(0, this._counter.);
//
//            await this._sut.Invoke(new DefaultHttpContext());
//            
//            Assert.AreEqual(1, this._counter.Value);
//        }
        
        [TestMethod]
        public async Task Given_request_populates_labels_correctly()
        {
            var counter = Metrics.CreateHistogram("all_labels_histogram", "", new[] {1d, 2d, 3d}, 
                "code", "method", "action", "controller");

            var expectedStatusCode = 400;
            var expectedMethod = "METHOD";
            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER";
            
            var hc = new DefaultHttpContext();
            SetupHttpContext(hc, expectedStatusCode, expectedMethod, expectedAction, expectedController);

            _sut = new HttpRequestDurationMiddleware(_requestDelegate, counter);

            await _sut.Invoke(hc);
            
            var labelValues = counter.Collect().Single().metric.Single().label;
            Assert.AreEqual(expectedStatusCode.ToString(), labelValues.Single(x => x.name == "code").value);
            Assert.AreEqual(expectedMethod, labelValues.Single(x => x.name == "method").value);
            Assert.AreEqual(expectedAction, labelValues.Single(x => x.name == "action").value);
            Assert.AreEqual(expectedController, labelValues.Single(x => x.name == "controller").value);
        }
        
        [TestMethod]
        public async Task Given_request_populates_labels_correctly2()
        {
            var counter = Metrics.CreateHistogram("all_labels_histogram", "", new[] {1d, 2d, 3d}, 
                "code", "method", "action", "controller");

            var expectedStatusCode = 400;
            var expectedMethod = "METHOD";
            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER";
            
            var hc = new DefaultHttpContext();
            SetupHttpContext(hc, expectedStatusCode, expectedMethod, expectedAction, expectedController);

            _sut = new HttpRequestDurationMiddleware(_requestDelegate, counter);

            await _sut.Invoke(hc);
            await _sut.Invoke(hc);
            
            hc.Response.StatusCode = 401;
            await _sut.Invoke(hc);
            
            var metric400 = counter.Collect().Single().metric.Single(x => x.label.Any(l => l.name == "code" && l.value == "400"));
            var metric401 = counter.Collect().Single().metric.Single(x => x.label.Any(l => l.name == "code" && l.value == "401"));

            Assert.AreEqual(2u, metric400.histogram.sample_count);
            Assert.AreEqual(1u, metric401.histogram.sample_count);
        }
        
        [TestMethod]
        public async Task Given_request_populates_labels_supplied_out_of_order_correctly()
        {
            var counter = Metrics.CreateHistogram("all_labels_histogram", "", new[] {1d, 2d, 3d}, 
                "code", "method", "action", "controller");

            var expectedStatusCode = 400;
            var expectedMethod = "METHOD";
            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER";
            
            var hc = new DefaultHttpContext();
            SetupHttpContext(hc, expectedStatusCode, expectedMethod, expectedAction, expectedController);

            _sut = new HttpRequestDurationMiddleware(_requestDelegate, counter);

            await _sut.Invoke(hc);
            
            var labelValues = counter.Collect().Single().metric.Single().label;
            Assert.AreEqual(expectedStatusCode.ToString(), labelValues.Single(x => x.name == "code").value);
            Assert.AreEqual(expectedMethod, labelValues.Single(x => x.name == "method").value);
            Assert.AreEqual(expectedAction, labelValues.Single(x => x.name == "action").value);
            Assert.AreEqual(expectedController, labelValues.Single(x => x.name == "controller").value);
        }
        
        [TestMethod]
        public async Task Given_request_populates_subset_of_labels_correctly()
        {
            var counter = Metrics.CreateHistogram("all_labels_histogram", "", new[] {1d, 2d, 3d}, 
                "action", "controller");

            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER";
            
            var hc = new DefaultHttpContext();
            SetupHttpContext(hc, 200, "", expectedAction, expectedController);

            _sut = new HttpRequestDurationMiddleware(_requestDelegate, counter);

            await _sut.Invoke(hc);
            
            var labelValues = counter.Collect().Single().metric.Single().label;
            Assert.IsFalse(labelValues.Any(x => x.name == "code"));
            Assert.IsFalse(labelValues.Any(x => x.name == "method"));
            Assert.AreEqual(expectedAction, labelValues.Single(x => x.name == "action").value);
            Assert.AreEqual(expectedController, labelValues.Single(x => x.name == "controller").value);
        }

        private static void SetupHttpContext(DefaultHttpContext hc, int expectedStatusCode, string expectedMethod,
            string expectedAction, string expectedController)
        {
            hc.Response.StatusCode = expectedStatusCode;
            hc.Request.Method = expectedMethod;

            hc.Features[typeof(IRoutingFeature)] = new FakeRoutingFeature
            {
                RouteData = new RouteData
                {
                    Values = {{"Action", expectedAction}, {"Controller", expectedController}}
                }
            };
        }

        [TestInitialize]
        public void Init()
        {
            DefaultCollectorRegistry.Instance.Clear();
            this._counter = Metrics.CreateHistogram("histogram", "");
            this._requestDelegate = context => Task.CompletedTask;

            _sut = new HttpRequestDurationMiddleware(this._requestDelegate, this._counter);
        }

        private HttpRequestDurationMiddleware _sut;
        private Histogram _counter;
        private RequestDelegate _requestDelegate;
    }
}