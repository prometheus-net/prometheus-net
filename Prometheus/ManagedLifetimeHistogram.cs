namespace Prometheus
{
    internal sealed class ManagedLifetimeHistogram : ManagedLifetimeMetricHandle<Histogram.Child, IHistogram>
    {
        public ManagedLifetimeHistogram(Collector<Histogram.Child> metric, TimeSpan expiresAfter) : base(metric, expiresAfter)
        {
        }

        public override ICollector<IHistogram> WithExtendLifetimeOnUse() => new AutoLeasingHistogram(this, _metric);
    }
}
