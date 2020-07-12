using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus.HttpClientMetrics
{
    internal sealed class
        HttpClientRequestDurationHandler : HttpClientDelegatingHandlerBase<ICollector<IHistogram>, IHistogram>
    {
        public HttpClientRequestDurationHandler(
            HttpClientRequestDurationOptions? options)
            : base(options, options?.Histogram)
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var stopWatch = Stopwatch.StartNew();
            try
            {
                return base.SendAsync(request, cancellationToken);
            }
            finally
            {
                stopWatch.Stop();

                CreateChild(request).Observe(stopWatch.Elapsed.TotalSeconds);
            }
        }

        protected override ICollector<IHistogram> CreateMetricInstance(string[] labelNames)
        {
            return MetricFactory.CreateHistogram(
                                                 "httpclient_request_duration_seconds",
                                                 "The duration of HTTP requests processed by a HttpClient.",
                                                 new HistogramConfiguration
                                                 {
                                                     // 1 ms to 32K ms buckets
                                                     Buckets = Histogram.ExponentialBuckets(0.001, 2, 16),
                                                     LabelNames = labelNames
                                                 });
        }
    }
}