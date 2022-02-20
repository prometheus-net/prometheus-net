using Prometheus;
using System;
using System.Net.Http;

namespace tester
{
    internal class MetricServerTester : Tester
    {
        public override IMetricServer InitializeMetricServer()
        {
            return new MetricServer(hostname: "localhost", port: TesterConstants.TesterPort);
        }

        private static readonly HttpClient _httpClient = new();

        public override void OnTimeToObserveMetrics()
        {
            var text = _httpClient.GetStringAsync($"http://localhost:{TesterConstants.TesterPort}/metrics").Result;
            Console.WriteLine(text);
        }
    }
}
