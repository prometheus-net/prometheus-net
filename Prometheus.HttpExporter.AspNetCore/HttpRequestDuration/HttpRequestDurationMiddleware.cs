using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Prometheus.HttpExporter.AspNetCore.HttpRequestDuration
{
    public class HttpRequestDurationMiddleware : HttpRequestMiddlewareBase<Histogram>
    {
        public HttpRequestDurationMiddleware(RequestDelegate next, Histogram histogram)
         : base(histogram)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            this.requestDuration = histogram;
        }

        public async Task Invoke(HttpContext context)
        {
            var stopWatch = new Stopwatch();
            
            stopWatch.Start();
            await this.next(context);
            stopWatch.Stop();

            var labelData = GetLabelData(context);
            
            if (labelData != null) {
                this.requestDuration
                    .WithLabels(labelData)
                    .Observe(stopWatch.ElapsedMilliseconds);
            }
        }

        private readonly RequestDelegate next;
        private readonly Histogram requestDuration;
    }
}