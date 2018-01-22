using System.Reactive.Concurrency;

namespace Prometheus
{
    public interface IMetricServer
    {
        void Start(IScheduler scheduler = null);
        void Stop();
    }
}
