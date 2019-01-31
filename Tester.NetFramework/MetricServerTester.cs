using Prometheus;
using System;
using System.IO;
using System.Net;

namespace tester
{
    internal class MetricServerTester : Tester
    {
        public override IMetricServer InitializeMetricServer()
        {
            return new MetricServer(hostname: "localhost", port: TesterConstants.TesterPort);
        }

        public override void OnTimeToObserveMetrics()
        {
            var httpRequest = (HttpWebRequest)WebRequest.Create($"http://localhost:{TesterConstants.TesterPort}/metrics");
            httpRequest.Method = "GET";

            using (var httpResponse = (HttpWebResponse)httpRequest.GetResponse())
            {
                var text = new StreamReader(httpResponse.GetResponseStream()).ReadToEnd();
                Console.WriteLine(text);
            }
        }
    }
}
