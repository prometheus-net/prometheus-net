namespace Prometheus
{
    public interface ICounter
    {
        void Inc(double increment = 1);
        void IncTo(double targetValue);
        double Value { get; }
    }
}
