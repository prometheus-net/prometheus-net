namespace Prometheus
{
    internal sealed class ManagedLifetimeGauge : ManagedLifetimeMetricHandle<Gauge.Child, IGauge>
    {
        public ManagedLifetimeGauge(Collector<Gauge.Child> metric, TimeSpan expiresAfter) : base(metric, expiresAfter)
        {
        }

        public override ICollector<IGauge> WithExtendLifetimeOnUse() => new AutoLeasingGauge(this, _metric);
    }
}
