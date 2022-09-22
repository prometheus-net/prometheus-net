namespace Prometheus
{
    internal sealed class ManagedLifetimeSummary : ManagedLifetimeMetricHandle<Summary.Child, ISummary>
    {
        public ManagedLifetimeSummary(Collector<Summary.Child> metric, TimeSpan expiresAfter) : base(metric, expiresAfter)
        {
        }

        public override ICollector<ISummary> WithExtendLifetimeOnUse() => new AutoLeasingSummary(this, _metric);
    }
}
