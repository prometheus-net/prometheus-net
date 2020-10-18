using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus.HttpClientMetrics
{
    public sealed class
        HttpClientInProgressHandler : HttpClientDelegatingHandlerBase<ICollector<IGauge>, IGauge>
    {
        public HttpClientInProgressHandler(HttpClientInProgressOptions? options)
            : base(options, options?.Gauge)
        {
        }


        public HttpClientInProgressHandler(HttpMessageHandler innerHandler,HttpClientInProgressOptions? options)
            : base(innerHandler, options, options?.Gauge)
        {
            
        }



        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            using (CreateChild(request).TrackInProgress())
            {
                return base.SendAsync(request, cancellationToken);
            }
        }

        protected override ICollector<IGauge> CreateMetricInstance(string[] labelNames)
        {
            return MetricFactory.CreateGauge(
                                             "httpclient_requests_in_progress",
                                             "The number of requests currently in progress in the HttpClient pipeline.",
                                             new GaugeConfiguration
                                             {
                                                 LabelNames = labelNames
                                             });
        }
    }
}