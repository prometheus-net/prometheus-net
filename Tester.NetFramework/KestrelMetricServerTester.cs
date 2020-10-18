using Prometheus;
using System;
using System.Security.Cryptography.X509Certificates;

namespace tester
{
    // Works ONLY on Tester.NetCore because Kestrel is a pain to get set up on NetFramework, so let's not bother.
    // You will get some libuv related error if you try to use Tester.NetFramework.
    internal class KestrelMetricServerTester : Tester
    {
        public KestrelMetricServerTester(string hostname = "localhost", X509Certificate2 certificate = null)
        {
        }

        public override IMetricServer InitializeMetricServer()
        {
            throw new NotImplementedException("Kestrel metric server is only available under .NET Core");
        }
    }
}
