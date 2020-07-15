using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus;
using Prometheus.HttpClientMetrics;

namespace Tests.Prometheus.HttpClient
{
    [TestClass]
    public class HttpClientInProgressHandlerTests
    {
        [TestMethod]
        public async Task when_i_request_a_uri_should_inc_inprogress_metric_and_dec_after_that()
        {
            //////////////////////////////////////
            // Arrange
            //////////////////////////////////////

            var gauge = Metrics.CreateGauge(
                                            "httpclient_requests_in_progress",
                                            "The number of requests currently in progress in the HttpClient pipeline.",
                                            new GaugeConfiguration
                                            {
                                                LabelNames = HttpClientRequestLabelNames.All
                                            });


            var captureGaugeValueHttpHandler = new CaptureGaugeValueHttpHandler();
            captureGaugeValueHttpHandler.Gauge = gauge;


            var options = new HttpClientInProgressOptions();
            options.Gauge = gauge;

            var httpClientInProgressHandler =
                new HttpClientInProgressHandler(captureGaugeValueHttpHandler, options);


            var httpClient = new System.Net.Http.HttpClient(httpClientInProgressHandler);


            //////////////////////////////////////
            // Act
            //////////////////////////////////////

            await httpClient.GetAsync("http://www.google.com").ConfigureAwait(false);

            //////////////////////////////////////
            // Assert
            //////////////////////////////////////


            Assert.AreEqual(1, captureGaugeValueHttpHandler.CapturedValue);
            Assert.AreEqual(0, gauge.Value);
        }

        internal class CaptureGaugeValueHttpHandler : DelegatingHandler
        {
            public CaptureGaugeValueHttpHandler()
            {
                InnerHandler = new HttpClientHandler();
            }

            public Gauge Gauge { get; set; }
            public double CapturedValue { get; set; }


            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                                                                   CancellationToken cancellationToken)
            {
                CapturedValue = Gauge.WithLabels("GET", "www.google.com").Value;
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}