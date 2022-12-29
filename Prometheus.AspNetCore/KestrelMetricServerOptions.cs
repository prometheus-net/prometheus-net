using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;

namespace Prometheus;

public sealed class KestrelMetricServerOptions
{
    /// <summary>
    /// Will listen for requests using this hostname. "+" indicates listen on all hostnames.
    /// By setting this to "localhost", you can easily prevent access from remote systems.-
    /// </summary>
    public string Hostname { get; set; } = "+";

    public ushort Port { get; set; }
    public string Url { get; set; } = "/metrics";
    public X509Certificate2? TlsCertificate { get; set; }

    // May be overridden by ConfigureExporter.
    [EditorBrowsable(EditorBrowsableState.Never)] // It is not exactly obsolete but let's de-emphasize it and prefer ConfigureExporter.
    public CollectorRegistry? Registry { get; set; }

    /// <summary>
    /// Allows metric exporter options to be configured in a flexible manner.
    /// The callback is called after applying any values in KestrelMetricServerOptions.
    /// </summary>
    public Action<MetricServerMiddleware.Settings>? ConfigureExporter { get; set; }
}
