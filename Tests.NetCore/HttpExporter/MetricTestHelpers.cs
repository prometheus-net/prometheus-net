using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Prometheus;
using Prometheus.DataContracts;
using System.Collections.Generic;
using System.Linq;

namespace Tests.HttpExporter
{
    public static class MetricTestHelpers
    {
        public static string GetLabelData(List<Metric> collectedMetrics, string labelName)
        {
            var labelValues = collectedMetrics.Single().label;
            return labelValues.SingleOrDefault(x => x.name == labelName)?.value;
        }

        public static double GetLabelCounterValue(List<Metric> collectedMetrics, string labelName, object labelValue)
        {
            return collectedMetrics
                .Single(x => x.label.Any(l => l.name == labelName && l.value == labelValue.ToString())).counter
                .value;
        }

        public static Prometheus.DataContracts.Histogram GetLabelHistogram(List<Metric> collectedMetrics, string labelName, object labelValue)
        {
            return collectedMetrics
                .Single(x => x.label.Any(l => l.name == labelName && l.value == labelValue.ToString())).histogram;
        }

        public static List<Metric> GetCollectedMetrics<T>(Collector<T> counter) where T : Child, new()
        {
            return counter.Collect().Single().metric;
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