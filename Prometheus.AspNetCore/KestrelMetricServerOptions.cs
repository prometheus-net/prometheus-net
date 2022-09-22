using System.Security.Cryptography.X509Certificates;

namespace Prometheus
{
    public sealed class KestrelMetricServerOptions
    {
        /// <summary>
        /// Will listen for requests using this hostname. "+" indicates listen on all hostnames.
        /// By setting this to "localhost", you can easily prevent access from remote systems.-
        /// </summary>
        public string Hostname { get; set; } = "+";

        public ushort Port { get; set; }
        public string Url { get; set; } = "/metrics";
        public CollectorRegistry? Registry { get; set; }
        public X509Certificate2? TlsCertificate { get; set; }
    }
}
