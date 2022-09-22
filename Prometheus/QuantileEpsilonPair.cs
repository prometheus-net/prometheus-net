namespace Prometheus
{
    public readonly struct QuantileEpsilonPair
    {
        public QuantileEpsilonPair(double quantile, double epsilon)
        {
            Quantile = quantile;
            Epsilon = epsilon;
        }

        public double Quantile { get; }
        public double Epsilon { get; }
    }
}
