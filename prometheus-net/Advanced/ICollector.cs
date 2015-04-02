namespace Prometheus.Advanced
{
    public interface ICollector
    {
        MetricFamily Collect();
        string Name { get; }
    }
}