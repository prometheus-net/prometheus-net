using Prometheus;
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace tester
{
    // Works ONLY on Tester.NetCore because Kestrel is a pain to get set up on NetFramework, so let's not bother.
    // You will get some libuv related error if you try to use Tester.NetFramework.
    internal class KestrelMetricServerTester : Tester
    {
        public KestrelMetricServerTester(string hostname = "localhost", X509Certificate2 certificate = null)
        {
            _hostname = hostname;
            _certificate = certificate;
        }

        private readonly string _hostname;
        private readonly X509Certificate2 _certificate;

        public override void OnTimeToObserveMetrics()
        {
            var url = $"http://{_hostname}:{TesterConstants.TesterPort}/metrics";

            if (_certificate != null)
                url = url.Replace("http://", "https://");

            var httpRequest = (HttpWebRequest)WebRequest.Create(url);
            httpRequest.Method = "GET";

            using (var httpResponse = (HttpWebResponse)httpRequest.GetResponse())
            {
                if (httpResponse.StatusCode != HttpStatusCode.OK)
                {
                    Console.WriteLine($"Response status code: {(int)httpResponse.StatusCode} {httpResponse.StatusCode} {httpResponse.StatusDescription}");
                    return;
                }

                var text = new StreamReader(httpResponse.GetResponseStream()).ReadToEnd();
                Console.WriteLine(text);
            }
        }

        public override IMetricServer InitializeMetricServer()
        {
            return new KestrelMetricServer(TesterConstants.TesterPort, certificate: _certificate);
        }
    }
}
