namespace Prometheus
{
    internal sealed class ManaggedLifetimeSummary : ManagedLifetimeMetricHandle<Summary.Child, ISummary>
    {
        public ManaggedLifetimeSummary(Collector<Summary.Child> metric, TimeSpan expiresAfter) : base(metric, expiresAfter)
        {
        }

        public override ICollector<ISummary> WithExtendLifetimeOnUse() => new AutoLeasingSummary(this, _metric);
    }
}
