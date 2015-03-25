namespace Prometheus.Internal
{
    internal interface IMetric
    {
        void Inc(double val = 1);
        void Dec(double val = 1);
        void Set(double val);
        IMetric WithLabel(string key, string value);
        double Value { get; }
    }
}