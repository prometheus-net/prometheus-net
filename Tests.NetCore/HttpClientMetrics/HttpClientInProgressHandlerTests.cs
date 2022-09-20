using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus.HttpClientMetrics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus.Tests.HttpClientMetrics
{
    [TestClass]
    public class HttpClientInProgressHandlerTests
    {
        [TestMethod]
        public async Task when_i_request_a_uri_should_inc_inprogress_metric_and_dec_after_that()
        {
            var registry = Metrics.NewCustomRegistry();

            var options = new HttpClientInProgressOptions
            {
                Registry = registry
            };

            var handler = new HttpClientInProgressHandler(options, HttpClientIdentity.Default);

            var gaugeInspectionHandler = new CaptureGaugeValueHttpHandler();
            gaugeInspectionHandler.Gauge = (Gauge)handler._metric;

            handler.InnerHandler = gaugeInspectionHandler;

            // As we are not using the HttpClientProvider for constructing our pipeline, we need to do this manually.
            gaugeInspectionHandler.InnerHandler = new HttpClientHandler();

            var client = new HttpClient(handler);
            await client.GetAsync(ConnectivityCheck.Url);

            Assert.AreEqual(1, gaugeInspectionHandler.CapturedValue);
            Assert.AreEqual(0, ((Gauge)handler._metric).Value);
        }

        private sealed class CaptureGaugeValueHttpHandler : DelegatingHandler
        {
            public Gauge Gauge { get; set; }
            public double CapturedValue { get; set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CapturedValue = Gauge.WithLabels("GET", ConnectivityCheck.Host, HttpClientIdentity.Default.Name, "").Value;
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}