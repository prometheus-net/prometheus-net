using Microsoft.AspNetCore.Http;

namespace Prometheus.HttpMetrics;

internal sealed class HttpRequestDurationMiddleware : HttpRequestMiddlewareBase<ICollector<IHistogram>, IHistogram>
{
    private readonly RequestDelegate _next;
    private readonly HttpRequestDurationOptions _options;

    public HttpRequestDurationMiddleware(RequestDelegate next, HttpRequestDurationOptions options)
        : base(options, options?.Histogram)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task Invoke(HttpContext context)
    {
        var stopWatch = ValueStopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            // We pass either null (== use default exemplar provider) or None (== do not record exemplar).
            Exemplar? exemplar = _options.ExemplarPredicate(context) ? null : Exemplar.None;
            
            CreateChild(context).Observe(stopWatch.GetElapsedTime().TotalSeconds, exemplar);
        }
    }

    protected override string[] BaselineLabels => HttpRequestLabelNames.Default;

    protected override ICollector<IHistogram> CreateMetricInstance(string[] labelNames) => MetricFactory.CreateHistogram(
        "http_request_duration_seconds",
        "The duration of HTTP requests processed by an ASP.NET Core application.",
        labelNames,
        new HistogramConfiguration
        {
            // 1 ms to 32K ms buckets
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 16),
        });
}