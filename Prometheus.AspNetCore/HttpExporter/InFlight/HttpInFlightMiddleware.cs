using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Prometheus.HttpExporter.InFlight
{
    public class HttpInFlightMiddleware
    {
        public HttpInFlightMiddleware(RequestDelegate next, HttpInFlightOptions options)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            this.inFlightGauge = Metrics.CreateGauge(options.MetricName, options.MetricDescription);
        }

        public async Task Invoke(HttpContext context)
        {
            this.inFlightGauge.Inc();
            
            await this.next(context);
            
            this.inFlightGauge.Dec();
        }
        
        private readonly RequestDelegate next;
        private readonly Gauge inFlightGauge;
    }
}