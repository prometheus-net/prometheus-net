using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace Prometheus.HttpMetrics
{
    public sealed class HttpInProgressMiddleware
    {
        private readonly IGauge inProgressGauge;

        private readonly RequestDelegate _next;

        public HttpInProgressMiddleware(RequestDelegate next, IGauge gauge)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));

            inProgressGauge = gauge;
        }

        public async Task Invoke(HttpContext context)
        {
            inProgressGauge.Inc();

            try
            {
                await _next(context);
            }
            finally
            {
                inProgressGauge.Dec();
            }
        }
    }
}