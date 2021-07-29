using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus.HttpClientMetrics
{
    internal sealed class HttpClientRequestCountHandler : HttpClientDelegatingHandlerBase<ICollector<ICounter>, ICounter>
    {
        public HttpClientRequestCountHandler(HttpClientRequestCountOptions? options, HttpClientIdentity identity)
            : base(options, options?.Counter, identity)
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CreateChild(request).Inc();
            return base.SendAsync(request, cancellationToken);
        }

        protected override ICollector<ICounter> CreateMetricInstance(string[] labelNames) => MetricFactory.CreateCounter(
            "httpclient_requests_received_total",
            "Count of HTTP requests that have been started by an HttpClient.",
            new CounterConfiguration
            {
                LabelNames = labelNames
            });
    }
}