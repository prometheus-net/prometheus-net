using System.Linq;
using Grpc.Core;
using Microsoft.AspNetCore.Http;

namespace Prometheus
{
    internal static class HttpResponseExtensions
    {
        private const string _grpcStatus = "grpc-status";

        public static StatusCode GetStatusCode(this HttpResponse response)
        {
            var headerExists = response.Headers.TryGetValue(_grpcStatus, out var header);

            if (!headerExists && response.StatusCode == StatusCodes.Status200OK)
            {
                return StatusCode.OK;
            }

            if (header.Any() && int.TryParse(header.FirstOrDefault(), out var status))
            {
                return (StatusCode)status;
            }

            return StatusCode.OK;
        }
    }
}