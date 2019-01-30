using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace Prometheus.AspNetCore.HttpExporter
{
    public class HttpInFlightMiddleware
    {
        private readonly IGauge _inFlightGauge;

        private readonly RequestDelegate _next;

        public HttpInFlightMiddleware(RequestDelegate next, IGauge gauge)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));

            _inFlightGauge = gauge;
        }

        public async Task Invoke(HttpContext context)
        {
            _inFlightGauge.Inc();

            try
            {
                await _next(context);
            }
            finally
            {
                _inFlightGauge.Dec();
            }
        }
    }
}