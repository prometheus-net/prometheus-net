using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus.HttpClientMetrics
{
    public sealed class
        HttpClientRequestCountHandler : HttpClientDelegatingHandlerBase<ICollector<ICounter>, ICounter>
    {
        public HttpClientRequestCountHandler(
            HttpClientRequestCountOptions? options)
            : base(options, options?.Counter)
        {
        }


        public HttpClientRequestCountHandler(HttpMessageHandler innerHandler, HttpClientRequestCountOptions? options)
            : base(innerHandler, options, options?.Counter)
        {

        }


        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CreateChild(request).Inc();
            return base.SendAsync(request, cancellationToken);
        }

        protected override ICollector<ICounter> CreateMetricInstance(string[] labelNames)
        {
            return MetricFactory.CreateCounter(
                                               "httpclient_requests_received_total",
                                               "Provides the count of HttpClient requests that have been called by the client.",
                                               new CounterConfiguration
                                               {
                                                   LabelNames = labelNames
                                               });
        }
    }
}