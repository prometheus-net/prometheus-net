using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Prometheus.HttpExporter.AspNetCore.InFlight
{
    public class HttpInFlightMiddleware
    {
        public HttpInFlightMiddleware(RequestDelegate next, IGauge gauge)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            
            _next = next;
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

        private readonly RequestDelegate _next;
        private readonly IGauge _inFlightGauge;
    }
}