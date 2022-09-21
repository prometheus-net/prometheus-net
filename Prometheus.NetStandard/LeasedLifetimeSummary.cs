namespace Prometheus
{
    internal sealed class LeasedLifetimeSummary : LeasedLifetimeMetric<Summary.Child, ISummary>
    {
        public LeasedLifetimeSummary(Collector<Summary.Child> metric, TimeSpan expiresAfter) : base(metric, expiresAfter)
        {
        }

        public override ICollector<ISummary> WithExtendLifetimeOnUse() => new AutoLeasingSummary(this, _metric);
    }
}
