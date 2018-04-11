using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Jobs;
using Prometheus;
using Prometheus.Advanced;

namespace Benchmark
{
    /// <summary>
    /// One pattern advocated by Prometheus documentation is to implement scraping of external systems by
    /// creating a brand new set of metrics for each scrape. So let's benchmark this scenario.
    /// </summary>
    [ClrJob]
    [MemoryDiagnoser]
    public class MetricCreationBenchmarks
    {
        /// <summary>
        /// Creating metrics involves placing them in a registry, which brings data size into the picture.
        /// For benchmarking this case, we create a number of them at once in the same registry.
        /// </summary>
        private const int _metricCount = 100;

        private const string _help = "arbitrary help message for metric, not relevant for benchmarking";

        private static readonly string[] _metricNames;

        static MetricCreationBenchmarks()
        {
            _metricNames = new string[_metricCount];

            for (var i = 0; i < _metricCount; i++)
                _metricNames[i] = $"metric_{i:D4}";
        }

        private ICollectorRegistry _registry;
        private MetricFactory _factory;

        [IterationSetup]
        public void Setup()
        {
            _registry = new DefaultCollectorRegistry();
            _factory = Metrics.WithCustomRegistry(_registry);
        }

        [Benchmark]
        public void Counter()
        {
            _factory.CreateCounter(_metricNames[0], _help).Inc();
        }

        [Benchmark]
        public void Gauge()
        {
            _factory.CreateGauge(_metricNames[0], _help).Inc();
        }

        [Benchmark]
        public void Summary()
        {
            _factory.CreateSummary(_metricNames[0], _help).Observe(123);
        }

        [Benchmark]
        public void Histogram()
        {
            _factory.CreateHistogram(_metricNames[0], _help).Observe(123);
        }

        [Benchmark]
        public void Counter_Many()
        {
            for (var i = 0; i < _metricCount; i++)
                _factory.CreateCounter(_metricNames[i], _help).Inc();
        }

        [Benchmark]
        public void Gauge_Many()
        {
            for (var i = 0; i < _metricCount; i++)
                _factory.CreateGauge(_metricNames[i], _help).Inc();
        }

        [Benchmark]
        public void Summary_Many()
        {
            for (var i = 0; i < _metricCount; i++)
                _factory.CreateSummary(_metricNames[i], _help).Observe(123);
        }

        [Benchmark]
        public void Histogram_Many()
        {
            for (var i = 0; i < _metricCount; i++)
                _factory.CreateHistogram(_metricNames[i], _help).Observe(123);
        }
    }
}
