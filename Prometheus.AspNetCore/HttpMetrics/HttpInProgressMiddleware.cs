using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace Prometheus.HttpMetrics
{
    public sealed class HttpInProgressMiddleware
    {
        private readonly IGauge _inProgressGauge;

        private readonly RequestDelegate _next;

        public HttpInProgressMiddleware(RequestDelegate next, IGauge gauge)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));

            _inProgressGauge = gauge;
        }

        public async Task Invoke(HttpContext context)
        {
            using (_inProgressGauge.TrackInProgress())
            {
                await _next(context);
            }
        }
    }
}