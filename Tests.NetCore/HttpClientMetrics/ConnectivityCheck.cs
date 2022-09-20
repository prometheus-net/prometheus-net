using System;

namespace Prometheus.Tests.HttpClientMetrics
{
    public static class ConnectivityCheck
    {
        public static readonly Uri Url = new Uri("http://www.msftncsi.com/ncsi.txt");
        public const string ExpectedResponseCode = "200";
        public const string Host = "www.msftncsi.com";
    }
}
