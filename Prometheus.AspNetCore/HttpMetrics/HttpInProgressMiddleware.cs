using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace Prometheus.HttpMetrics
{
    internal sealed class HttpInProgressMiddleware : HttpRequestMiddlewareBase<ICollector<IGauge>, IGauge>
    {
        private readonly RequestDelegate _next;

        public HttpInProgressMiddleware(RequestDelegate next, HttpInProgressOptions? options)
            : base(options, options?.Gauge)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public async Task Invoke(HttpContext context)
        {
            // In ASP.NET Core 2, we will not have route data, so we cannot record controller/action labels.
            // In ASP.NET Core 3, we will have this data and can record the labels.
            // CreateChild() will take care of applying the right labels, no need to think hard about it here.
            using (CreateChild(context).TrackInProgress())
            {
                await _next(context);
            }
        }

        protected override string[] DefaultLabels => HttpRequestLabelNames.PotentiallyAvailableBeforeExecutingFinalHandler;

        protected override ICollector<IGauge> CreateMetricInstance(string[] labelNames) => MetricFactory.CreateGauge(
            "http_requests_in_progress",
            "The number of requests currently in progress in the ASP.NET Core pipeline. One series without controller/action label values counts all in-progress requests, with separate series existing for each controller-action pair.",
            new GaugeConfiguration
            {
                LabelNames = labelNames
            });
    }
}