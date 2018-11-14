namespace Prometheus
{
    public interface IObserver
    {
        void Observe(double val);
    }
}