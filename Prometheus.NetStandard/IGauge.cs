namespace Prometheus
{
    public interface IGauge : ICollectorChild
    {
        void Inc(double increment = 1);
        void Set(double val);
        void Dec(double decrement = 1);
        void IncTo(double targetValue);
        void DecTo(double targetValue);
        double Value { get; }
    }
}
