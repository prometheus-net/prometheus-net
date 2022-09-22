namespace Prometheus
{
    internal sealed class ManagedLifetimeCounter : ManagedLifetimeMetricHandle<Counter.Child, ICounter>
    {
        public ManagedLifetimeCounter(Collector<Counter.Child> metric, TimeSpan expiresAfter) : base(metric, expiresAfter)
        {
        }

        public override ICollector<ICounter> WithExtendLifetimeOnUse() => new AutoLeasingCounter(this, _metric);
    }
}
