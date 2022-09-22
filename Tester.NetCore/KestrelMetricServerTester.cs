using Prometheus;
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

        private static readonly HttpClient _httpClient = new();

        public override void OnTimeToObserveMetrics()
        {
            var url = $"http://{_hostname}:{TesterConstants.TesterPort}/metrics";

            if (_certificate != null)
                url = url.Replace("http://", "https://");

            var response = _httpClient.GetAsync($"http://localhost:{TesterConstants.TesterPort}/metrics").Result;

            if (response.StatusCode != HttpStatusCode.OK)
            {
                Console.WriteLine($"Response status code: {(int)response.StatusCode} {response.StatusCode}");
                return;
            }

            var text = response.Content.ReadAsStringAsync().Result;
            Console.WriteLine(text);
        }

        public override IMetricServer InitializeMetricServer()
        {
            return new KestrelMetricServer(TesterConstants.TesterPort, certificate: _certificate);
        }
    }
}
