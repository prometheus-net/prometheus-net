namespace Prometheus.Advanced
{
    public class MetricFactory
    {
        private readonly ICollectorRegistry _registry;

        public MetricFactory(ICollectorRegistry registry)
        {
            _registry = registry;
        }

        public Prometheus.Counter CreateCounter(string name, string help, params string[] labelNames)
        {
            var metric = new Prometheus.Counter(name, help, labelNames);
            _registry.Register(metric);
            return metric;
        }

        public Prometheus.Gauge CreateGauge(string name, string help, params string[] labelNames)
        {
            var metric = new Prometheus.Gauge(name, help, labelNames);
            _registry.Register(metric);
            return metric;
        }

        public Prometheus.Summary CreateSummary(string name, string help, params string[] labelNames)
        {
            var metric = new Prometheus.Summary(name, help, labelNames);
            _registry.Register(metric);
            return metric;
        }

        public Prometheus.Histogram CreateHistogram(string name, string help, double[] buckets = null, params string[] labelNames)
        {
            var metric = new Prometheus.Histogram(name, help, labelNames, buckets);
            _registry.Register(metric);
            return metric;
        }
    }
}