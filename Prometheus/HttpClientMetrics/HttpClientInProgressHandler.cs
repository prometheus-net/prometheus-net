namespace Prometheus.HttpClientMetrics;

internal sealed class HttpClientInProgressHandler : HttpClientDelegatingHandlerBase<ICollector<IGauge>, IGauge>
{
    public HttpClientInProgressHandler(HttpClientInProgressOptions? options, HttpClientIdentity identity)
        : base(options, options?.Gauge, identity)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (CreateChild(request, null).TrackInProgress())
        {
            // Returns when the response HEADERS are seen.
            return await base.SendAsync(request, cancellationToken);
        }
    }

    protected override string[] DefaultLabels => HttpClientRequestLabelNames.KnownInAdvance;

    protected override ICollector<IGauge> CreateMetricInstance(string[] labelNames) => MetricFactory.CreateGauge(
        "httpclient_requests_in_progress",
        "Number of requests currently being executed by an HttpClient that have not yet received response headers. Value is decremented once response headers are received.",
        labelNames);
}