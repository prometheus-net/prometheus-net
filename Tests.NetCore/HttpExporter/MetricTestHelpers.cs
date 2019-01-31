using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Prometheus;
using System.Collections.Generic;
using System.Linq;

namespace Tests.HttpExporter
{
    public static class MetricTestHelpers
    {
        internal static string GetLabelData(List<MetricData> collectedMetrics, string labelName)
        {
            var labelValues = collectedMetrics.Single().Labels;
            return labelValues.SingleOrDefault(x => x.Name == labelName)?.Value;
        }

        internal static double GetLabelCounterValue(List<MetricData> collectedMetrics, string labelName, object labelValue)
        {
            return collectedMetrics
                .Single(x => x.Labels.Any(l => l.Name == labelName && l.Value == labelValue.ToString())).Counter
                .Value;
        }

        internal static HistogramData GetLabelHistogram(List<MetricData> collectedMetrics, string labelName, object labelValue)
        {
            return collectedMetrics
                .Single(x => x.Labels.Any(l => l.Name == labelName && l.Value == labelValue.ToString())).Histogram;
        }

        internal static List<MetricData> GetCollectedMetrics<T>(Collector<T> counter) where T : ChildBase, new()
        {
            return counter.Collect().Metrics;
        }

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
    }
}