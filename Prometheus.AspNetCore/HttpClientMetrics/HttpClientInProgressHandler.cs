using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus.HttpClientMetrics
{
    internal sealed class HttpClientInProgressHandler : HttpClientDelegatingHandlerBase<ICollector<IGauge>, IGauge>
    {
        public HttpClientInProgressHandler(HttpClientInProgressOptions? options)
            : base(options, options?.Gauge)
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using (CreateChild(request).TrackInProgress())
            {
                return base.SendAsync(request, cancellationToken);
            }
        }

        protected override ICollector<IGauge> CreateMetricInstance(string[] labelNames) => MetricFactory.CreateGauge(
            "httpclient_requests_in_progress",
            "Number of requests currently being executed by an HttpClient.",
            new GaugeConfiguration
            {
                LabelNames = labelNames
            });
    }
}