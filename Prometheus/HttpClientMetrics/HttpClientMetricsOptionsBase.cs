namespace Prometheus.HttpClientMetrics;

public abstract class HttpClientMetricsOptionsBase
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Allows you to override the registry used to create the default metric instance.
    /// Value is ignored if you specify a custom metric instance in the options.
    /// </summary>
    public CollectorRegistry? Registry { get; set; }
}