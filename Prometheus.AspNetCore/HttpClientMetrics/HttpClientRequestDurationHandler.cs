using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus.HttpClientMetrics
{
    internal sealed class HttpClientRequestDurationHandler : HttpClientDelegatingHandlerBase<ICollector<IHistogram>, IHistogram>
    {
        public HttpClientRequestDurationHandler(HttpClientRequestDurationOptions? options)
            : base(options, options?.Histogram)
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var stopWatch = ValueStopwatch.StartNew();

            try
            {
                return await base.SendAsync(request, cancellationToken);
            }
            finally
            {
                CreateChild(request).Observe(stopWatch.GetElapsedTime().TotalSeconds);
            }
        }

        protected override ICollector<IHistogram> CreateMetricInstance(string[] labelNames) => MetricFactory.CreateHistogram(
            "httpclient_request_duration_seconds",
            "Duration histogram of HTTP requests performed by an HttpClient.",
            new HistogramConfiguration
            {
                // 1 ms to 32K ms buckets
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 16),
                LabelNames = labelNames
            });
    }
}