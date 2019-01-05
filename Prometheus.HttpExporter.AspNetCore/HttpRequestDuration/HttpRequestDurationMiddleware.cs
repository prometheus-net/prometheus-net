using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Prometheus.HttpExporter.AspNetCore.HttpRequestDuration
{
    public class HttpRequestDurationMiddleware : HttpRequestMiddlewareBase<Histogram>
    {
        public HttpRequestDurationMiddleware(RequestDelegate next, Histogram histogram)
         : base(histogram)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _requestDuration = histogram;
        }

        public async Task Invoke(HttpContext context)
        {
            var stopWatch = Stopwatch.StartNew();
            
            try
            {
                await _next(context);
            }
            finally
            {
                stopWatch.Stop();

                var labelData = GetLabelData(context);
            
                if (labelData != null) {
                    _requestDuration
                        .WithLabels(labelData)
                        .Observe(stopWatch.ElapsedMilliseconds);
                } 
            }
        }

        private readonly RequestDelegate _next;
        private readonly Histogram _requestDuration;
    }
}