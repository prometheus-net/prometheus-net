namespace Prometheus.HttpClientMetrics;

internal sealed class HttpClientRequestDurationHandler : HttpClientDelegatingHandlerBase<ICollector<IHistogram>, IHistogram>
{
    public HttpClientRequestDurationHandler(HttpClientRequestDurationOptions? options, HttpClientIdentity identity)
        : base(options, options?.Histogram, identity)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var stopWatch = ValueStopwatch.StartNew();

        HttpResponseMessage? response = null;

        try
        {
            // We measure until SendAsync returns - which is when the response HEADERS are seen.
            // The response body may continue streaming for a long time afterwards, which this does not measure.
            response = await base.SendAsync(request, cancellationToken);
            return response;
        }
        finally
        {
            CreateChild(request, response).Observe(stopWatch.GetElapsedTime().TotalSeconds);
        }
    }

    protected override string[] DefaultLabels => HttpClientRequestLabelNames.All;

    protected override ICollector<IHistogram> CreateMetricInstance(string[] labelNames) => MetricFactory.CreateHistogram(
        "httpclient_request_duration_seconds",
        "Duration histogram of HTTP requests performed by an HttpClient.",
        labelNames,
        new HistogramConfiguration
        {
            // 1 ms to 32K ms buckets
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 16),
        });
}