namespace Prometheus;

public readonly struct QuantileEpsilonPair(double quantile, double epsilon)
{
    public double Quantile { get; } = quantile;
    public double Epsilon { get; } = epsilon;
}
