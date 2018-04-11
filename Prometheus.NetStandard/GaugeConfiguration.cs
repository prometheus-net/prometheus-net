namespace Prometheus
{
    public sealed class GaugeConfiguration : MetricConfiguration
    {
        internal static readonly GaugeConfiguration Default = new GaugeConfiguration();
    }
}
