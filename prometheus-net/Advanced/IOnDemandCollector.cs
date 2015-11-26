namespace Prometheus.Advanced
{
    public interface IOnDemandCollector
    {
        void RegisterMetrics();
        void UpdateMetrics();
    }
}