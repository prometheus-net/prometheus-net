namespace Prometheus
{
    internal sealed class LeasedLifetimeHistogram : LeasedLifetimeMetric<Histogram.Child, IHistogram>
    {
        public LeasedLifetimeHistogram(Collector<Histogram.Child> metric, TimeSpan expiresAfter) : base(metric, expiresAfter)
        {
        }

        public override ICollector<IHistogram> WithExtendLifetimeOnUse() => new AutoLeasingHistogram(this, _metric);
    }
}
