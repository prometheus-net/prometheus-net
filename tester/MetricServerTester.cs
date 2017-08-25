using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Prometheus;

namespace tester
{
    class MetricServerTester : Tester
    {
        public override IMetricServer InitializeMetricHandler()
        {
            return new MetricServer(hostname: "localhost", port: 21881);
        }

        public override void OnObservation()
        {
            var httpRequest = (HttpWebRequest)WebRequest.Create("http://localhost:21881/metrics");
            httpRequest.Method = "GET";

            using (var httpResponse = (HttpWebResponse)httpRequest.GetResponse())
            {
                var text = new StreamReader(httpResponse.GetResponseStream()).ReadToEnd();
                Console.WriteLine(text);
            }
        }
    }
}
