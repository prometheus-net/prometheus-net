namespace Prometheus
{
    internal sealed class LeasedLifetimeGauge : LeasedLifetimeMetric<Gauge.Child, IGauge>
    {
        public LeasedLifetimeGauge(Collector<Gauge.Child> metric, TimeSpan expiresAfter) : base(metric, expiresAfter)
        {
        }

        public override ICollector<IGauge> WithExtendLifetimeOnUse() => new AutoLeasingGauge(this, _metric);
    }
}
