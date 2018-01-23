using System.Threading.Tasks;

namespace Prometheus
{
    /// <summary>
    /// A metric server exposes a Prometheus metric exporter endpoint in the background,
    /// operating independently and serving metrics until it is instructed to stop.
    /// </summary>
    public interface IMetricServer
    {
        void Start();

        Task StopAsync();
    }
}
