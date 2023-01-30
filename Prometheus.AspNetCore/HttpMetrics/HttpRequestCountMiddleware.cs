using Microsoft.AspNetCore.Http;

namespace Prometheus.HttpMetrics;

internal sealed class HttpRequestCountMiddleware : HttpRequestMiddlewareBase<ICollector<ICounter>, ICounter>
{
    private readonly RequestDelegate _next;
    private readonly HttpRequestCountOptions _options;

    public HttpRequestCountMiddleware(RequestDelegate next, HttpRequestCountOptions options)
        : base(options, options?.Counter)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        finally
        {
            // We pass either null (== use default exemplar provider) or None (== do not record exemplar).
            Exemplar? exemplar = _options.ExemplarPredicate(context) ? null : Exemplar.None;

            CreateChild(context).Inc(exemplar);
        }
    }

    protected override string[] BaselineLabels => HttpRequestLabelNames.Default;

    protected override ICollector<ICounter> CreateMetricInstance(string[] labelNames) => MetricFactory.CreateCounter(
        "http_requests_received_total",
        "Provides the count of HTTP requests that have been processed by the ASP.NET Core pipeline.",
        labelNames);
}