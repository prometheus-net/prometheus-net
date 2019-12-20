using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace Prometheus.HttpMetrics
{
    public sealed class HttpInProgressMiddleware : HttpRequestMiddlewareBase<ICollector<IGauge>, IGauge>
    {
        private readonly RequestDelegate _next;

        protected override string[] AllowedLabelNames => HttpRequestLabelNames.PotentiallyAvailableBeforeExecutingFinalHandler;

        public HttpInProgressMiddleware(RequestDelegate next, ICollector<IGauge> gauge)
            : base(gauge)
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
    }
}