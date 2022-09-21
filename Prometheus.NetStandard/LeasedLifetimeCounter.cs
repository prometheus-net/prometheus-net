namespace Prometheus
{
    internal sealed class LeasedLifetimeCounter : LeasedLifetimeMetric<Counter.Child, ICounter>
    {
        public LeasedLifetimeCounter(Collector<Counter.Child> metric, TimeSpan expiresAfter) : base(metric, expiresAfter)
        {
        }

        public override ICollector<ICounter> WithExtendLifetimeOnUse() => new AutoLeasingCounter(this, _metric);
    }
}
