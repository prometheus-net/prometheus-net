using Prometheus.Advanced.DataContracts;

namespace Prometheus.Advanced
{
    public interface ICollector
    {
        MetricFamily Collect();
        string Name { get; }

        string[] LabelNames { get; }
    }
}