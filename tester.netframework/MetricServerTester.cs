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
    class MetricServerTester 
    {
        public IMetricServer InitializeMetricHandler()
        {
            return new MetricServer(hostname: "localhost", port: 1234);
        }

        public void OnObservation()
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
