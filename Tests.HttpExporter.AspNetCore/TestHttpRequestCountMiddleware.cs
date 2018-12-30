using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus;
using Prometheus.Advanced;
using Prometheus.HttpExporter.AspNetCore.HttpRequestCount;
using Counter = Prometheus.Counter;

namespace Tests.HttpExporter.AspNetCore
{
    [TestClass]
    public class TestHttpRequestCountMiddleware
    {
        [TestMethod]
        public void Given_null_counter_then_throws()
        {
            Assert.ThrowsException<ArgumentException>(() =>
                new HttpRequestCountMiddleware(this._requestDelegate, null));
        }
        
        [TestMethod]
        public void Given_invalid_labels_then_throws()
        {
            var counter = Metrics.CreateCounter("invalid_labels_counter", "", "invalid");

            Assert.ThrowsException<ArgumentException>(() =>
                new HttpRequestCountMiddleware(this._requestDelegate, counter));
        }
        
        [TestMethod]
        public async Task Given_request_populates_labels_correctly()
        {
            var counter = Metrics.CreateCounter("all_labels_counter", "", "code", "method", "action", "controller");

            var expectedStatusCode = 400;
            var expectedMethod = "METHOD";
            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER";
            
            var hc = new DefaultHttpContext();
            SetupHttpContext(hc, expectedStatusCode, expectedMethod, expectedAction, expectedController);

            _sut = new HttpRequestCountMiddleware(_requestDelegate, counter);

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
            var counter = Metrics.CreateCounter("all_labels_counter", "", "code", "method", "action", "controller");

            var expectedStatusCode = 400;
            var expectedMethod = "METHOD";
            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER";
            
            var hc = new DefaultHttpContext();
            SetupHttpContext(hc, expectedStatusCode, expectedMethod, expectedAction, expectedController);

            _sut = new HttpRequestCountMiddleware(_requestDelegate, counter);

            await _sut.Invoke(hc);
            await _sut.Invoke(hc);
            
            hc.Response.StatusCode = 401;
            await _sut.Invoke(hc);
            
            var metric400 = counter.Collect().Single().metric.Single(x => x.label.Any(l => l.name == "code" && l.value == "400"));
            var metric401 = counter.Collect().Single().metric.Single(x => x.label.Any(l => l.name == "code" && l.value == "401"));

            Assert.AreEqual(2, metric400.counter.value);
            Assert.AreEqual(1, metric401.counter.value);
        }
        
        [TestMethod]
        public async Task Given_request_populates_labels_supplied_out_of_order_correctly()
        {
            var counter = Metrics.CreateCounter("all_labels_counter", "", "action", "code", "method", "controller");

            var expectedStatusCode = 400;
            var expectedMethod = "METHOD";
            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER";
            
            var hc = new DefaultHttpContext();
            SetupHttpContext(hc, expectedStatusCode, expectedMethod, expectedAction, expectedController);

            _sut = new HttpRequestCountMiddleware(_requestDelegate, counter);

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
            var counter = Metrics.CreateCounter("all_labels_counter", "", "action", "controller");

            var expectedAction = "ACTION";
            var expectedController = "CONTROLLER";
            
            var hc = new DefaultHttpContext();
            SetupHttpContext(hc, 200, "", expectedAction, expectedController);

            _sut = new HttpRequestCountMiddleware(_requestDelegate, counter);

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
            this._counter = Metrics.CreateCounter("counter", "");
            this._requestDelegate = context => Task.CompletedTask;

            _sut = new HttpRequestCountMiddleware(this._requestDelegate, this._counter);
        }

        private HttpRequestCountMiddleware _sut;
        private Counter _counter;
        private RequestDelegate _requestDelegate;
    }

    class FakeRoutingFeature : IRoutingFeature
    {
        public RouteData RouteData { get; set; }
    }
}