namespace Prometheus.SummaryImpl
{
    public struct QuantileEpsilonPair
    {
        public QuantileEpsilonPair(double quantile, double epsilon)
        {
            Quantile = quantile;
            Epsilon = epsilon;
        }

        public double Quantile { get; private set; }
        public double Epsilon { get; private set; }
    }
}
