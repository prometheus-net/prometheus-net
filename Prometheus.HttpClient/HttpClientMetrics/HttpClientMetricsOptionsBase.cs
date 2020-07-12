namespace Prometheus.HttpClientMetrics
{
    public abstract class HttpClientMetricsOptionsBase
    {
        public bool Enabled { get; set; } = true;
        /// <summary>
        /// Allows you to override the registry used to create the default metric instance.
        /// </summary>
        public CollectorRegistry? Registry { get; set; }
    }
}