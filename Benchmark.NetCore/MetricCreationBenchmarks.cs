using BenchmarkDotNet.Attributes;
using Prometheus;

namespace Benchmark.NetCore
{
    /// <summary>
    /// One pattern advocated by Prometheus documentation is to implement scraping of external systems by
    /// creating a brand new set of metrics for each scrape. So let's benchmark this scenario.
    /// </summary>
    [MemoryDiagnoser]
    public class MetricCreationBenchmarks
    {
        /// <summary>
        /// Just to ensure that a benchmark iteration has enough to do for stable and meaningful results.
        /// </summary>
        private const int _metricCount = 10000;

        /// <summary>
        /// Some benchmarks try to register metrics that already exist.
        /// </summary>
        private const int _duplicateCount = 5;

        private const string _help = "arbitrary help message for metric, not relevant for benchmarking";

        private static readonly string[] _metricNames;

        static MetricCreationBenchmarks()
        {
            _metricNames = new string[_metricCount];

            for (var i = 0; i < _metricCount; i++)
                _metricNames[i] = $"metric_{i:D4}";
        }

        private CollectorRegistry _registry;
        private MetricFactory _factory;

        [IterationSetup]
        public void Setup()
        {
            _registry = Metrics.NewCustomRegistry();
            _factory = Metrics.WithCustomRegistry(_registry);
        }

        private static readonly string[] _labelNames = new[] { "foo", "bar", "baz" };

        [Benchmark]
        public void Counter_Many()
        {
            for (var i = 0; i < _metricCount; i++)
                _factory.CreateCounter(_metricNames[i], _help, new CounterConfiguration
                {
                    LabelNames = _labelNames
                }).Inc();
        }

        [Benchmark]
        public void Gauge_Many()
        {
            for (var i = 0; i < _metricCount; i++)
                _factory.CreateGauge(_metricNames[i], _help, new GaugeConfiguration
                {
                    LabelNames = _labelNames
                }).Inc();
        }

        [Benchmark]
        public void Summary_Many()
        {
            for (var i = 0; i < _metricCount; i++)
                _factory.CreateSummary(_metricNames[i], _help, new SummaryConfiguration
                {
                    LabelNames = _labelNames
                }).Observe(123);
        }

        [Benchmark]
        public void Histogram_Many()
        {
            for (var i = 0; i < _metricCount; i++)
                _factory.CreateHistogram(_metricNames[i], _help, new HistogramConfiguration
                {
                    LabelNames = _labelNames
                }).Observe(123);
        }

        [Benchmark]
        public void Counter_Many_Duplicates()
        {
            for (var dupe = 0; dupe < _duplicateCount; dupe++)
                for (var i = 0; i < _metricCount; i++)
                    _factory.CreateCounter(_metricNames[i], _help, new CounterConfiguration
                    {
                        LabelNames = _labelNames
                    }).Inc();
        }

        [Benchmark]
        public void Gauge_Many_Duplicates()
        {
            for (var dupe = 0; dupe < _duplicateCount; dupe++)
                for (var i = 0; i < _metricCount; i++)
                    _factory.CreateGauge(_metricNames[i], _help, new GaugeConfiguration
                    {
                        LabelNames = _labelNames
                    }).Inc();
        }

        [Benchmark]
        public void Summary_Many_Duplicates()
        {
            for (var dupe = 0; dupe < _duplicateCount; dupe++)
                for (var i = 0; i < _metricCount; i++)
                    _factory.CreateSummary(_metricNames[i], _help, new SummaryConfiguration
                    {
                        LabelNames = _labelNames
                    }).Observe(123);
        }

        [Benchmark]
        public void Histogram_Many_Duplicates()
        {
            for (var dupe = 0; dupe < _duplicateCount; dupe++)
                for (var i = 0; i < _metricCount; i++)
                    _factory.CreateHistogram(_metricNames[i], _help, new HistogramConfiguration
                    {
                        LabelNames = _labelNames
                    }).Observe(123);
        }

        [Benchmark]
        public void Counter()
        {
            _factory.CreateCounter(_metricNames[0], _help, new CounterConfiguration
            {
                LabelNames = _labelNames
            }).Inc();
        }

        [Benchmark]
        public void Gauge()
        {
            _factory.CreateGauge(_metricNames[0], _help, new GaugeConfiguration
            {
                LabelNames = _labelNames
            }).Inc();
        }

        [Benchmark]
        public void Summary()
        {
            _factory.CreateSummary(_metricNames[0], _help, new SummaryConfiguration
            {
                LabelNames = _labelNames
            }).Observe(123);
        }

        [Benchmark]
        public void Histogram()
        {
            _factory.CreateHistogram(_metricNames[0], _help, new HistogramConfiguration
            {
                LabelNames = _labelNames
            }).Observe(123);
        }

        [Benchmark]
        public void Counter_Duplicates()
        {
            for (var dupe = 0; dupe < _duplicateCount; dupe++)
                _factory.CreateCounter(_metricNames[0], _help, new CounterConfiguration
                {
                    LabelNames = _labelNames
                }).Inc();
        }

        [Benchmark]
        public void Gauge_Duplicates()
        {
            for (var dupe = 0; dupe < _duplicateCount; dupe++)
                _factory.CreateGauge(_metricNames[0], _help, new GaugeConfiguration
                {
                    LabelNames = _labelNames
                }).Inc();
        }

        [Benchmark]
        public void Summary_Duplicates()
        {
            for (var dupe = 0; dupe < _duplicateCount; dupe++)
                _factory.CreateSummary(_metricNames[0], _help, new SummaryConfiguration
                {
                    LabelNames = _labelNames
                }).Observe(123);
        }

        [Benchmark]
        public void Histogram_Duplicates()
        {
            for (var dupe = 0; dupe < _duplicateCount; dupe++)
                _factory.CreateHistogram(_metricNames[0], _help, new HistogramConfiguration
                {
                    LabelNames = _labelNames
                }).Observe(123);
        }
    }
}
