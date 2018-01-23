using Prometheus;
using System;
using System.IO;
using System.Net;

namespace tester
{
    class MetricServerTester : Tester
    {
        public override IMetricServer InitializeMetricServer()
        {
            return new MetricServer(hostname: "localhost", port: 1234);
        }

        public override void OnTimeToObserveMetrics()
        {
            var httpRequest = (HttpWebRequest)WebRequest.Create("http://localhost:1234/metrics");
            httpRequest.Method = "GET";

            using (var httpResponse = (HttpWebResponse)httpRequest.GetResponse())
            {
                var text = new StreamReader(httpResponse.GetResponseStream()).ReadToEnd();
                Console.WriteLine(text);
            }
        }
    }
}
