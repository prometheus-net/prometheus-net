using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Prometheus.HttpExporter.AspNetCore.HttpRequestCount
{
    public class HttpRequestCountMiddleware : HttpRequestMiddlewareBase<Counter, Counter.Child>
    {
        public HttpRequestCountMiddleware(RequestDelegate next, Counter counter)
            : base(counter)            
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            if (counter == null) throw new ArgumentException(nameof(counter));

            requestCount = counter;
        }

        public async Task Invoke(HttpContext context)
        {
            await this.next(context);

            var labels = GetLabelData(context);

            if (labels != null) this.requestCount.WithLabels(labels).Inc();
        }

        private readonly RequestDelegate next;
        private readonly Counter requestCount;
    }
}