using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace Prometheus.Grpc
{
    /// <summary>
    /// Counts the number of requests to gRPC services.
    /// </summary>
    public sealed class GrpcRequestCountMiddleware : GrpcRequestMiddlewareBase<ICollector<ICounter>, ICounter>
    {
        private readonly RequestDelegate _next;

        protected override string[] AllowedLabelNames => GrpcRequestLabelNames.All;

        public GrpcRequestCountMiddleware(RequestDelegate next, ICollector<ICounter> counter)
            : base(counter)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            finally
            {
                CreateChild(context)?.Inc();
            }
        }
    }
}