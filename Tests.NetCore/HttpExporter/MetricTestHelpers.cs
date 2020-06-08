using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Prometheus;
using System.Collections.Generic;
using System.Linq;

namespace Tests.HttpExporter
{
    public static class MetricTestHelpers
    {
        public static void SetupHttpContext(DefaultHttpContext hc, int expectedStatusCode, string expectedMethod,
            string expectedAction, string expectedController)
        {
            hc.Response.StatusCode = expectedStatusCode;
            hc.Request.Method = expectedMethod;

            hc.Features[typeof(IRoutingFeature)] = new FakeRoutingFeature
            {
                RouteData = new RouteData
                {
                    Values = { { "Action", expectedAction }, { "Controller", expectedController } }
                }
            };
        }

        internal static string GetLabelValueOrDefault(Labels labels, string name)
        {
            return labels.Names
                .Zip(labels.Values, (n, v) => (n, v))
                .FirstOrDefault(pair => pair.n == name).v;
        }

        internal static string[] GetLabelValues(IEnumerable<Labels> labels, string name)
        {
            return labels.SelectMany(l => l.Names
                    .Zip(l.Values, (n, v) => (n, v))
                    .Where(pair => pair.n == name)
                    .Select(pair => pair.v))
                    .ToArray();
        }
    }
}