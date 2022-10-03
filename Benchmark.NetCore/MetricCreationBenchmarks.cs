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
        private const int _metricCount = 100;

        /// <summary>
        /// Some benchmarks try to register metrics that already exist.
        /// </summary>
        private const int _duplicateCount = 10;

        /// <summary>
        /// How many times we repeat acquiring and incrementing the same instance.
        /// </summary>
        [Params(1, 10)]
        public int RepeatCount { get; set; }

        [Params(true, false)]
        public bool IncludeStaticLabels { get; set; }

        private const string _help = "arbitrary help message for metric, not relevant for benchmarking";

        private static readonly string[] _metricNames;

        static MetricCreationBenchmarks()
        {
            _metricNames = new string[_metricCount];

            for (var i = 0; i < _metricCount; i++)
                _metricNames[i] = $"metric_{i:D4}";
        }

        private CollectorRegistry _registry;
        private IMetricFactory _factory;

        [IterationSetup]
        public void Setup()
        {
            _registry = Metrics.NewCustomRegistry();
            _factory = Metrics.WithCustomRegistry(_registry);

            if (IncludeStaticLabels)
            {
                _registry.SetStaticLabels(new Dictionary<string, string>
                {
                    { "static_foo", "static_bar" },
                    { "static_foo1", "static_bar" },
                    { "static_foo2", "static_bar" },
                    { "static_foo3", "static_bar" },
                    { "static_foo4", "static_bar" }
                });

                _factory = _factory.WithLabels(new Dictionary<string, string>
                {
                    { "static_gaa", "static_bar" },
                    { "static_gaa1", "static_bar" },
                    { "static_gaa2", "static_bar" },
                    { "static_gaa3", "static_bar" },
                    { "static_gaa4", "static_bar" },
                    { "static_gaa5", "static_bar" },
                });
            }
        }

        // We use the same strings both for the names and the values.
        private static readonly string[] _labels = new[] { "foo", "bar", "baz" };

        [Benchmark]
        public void Counter_Many()
        {
            for (var i = 0; i < _metricCount; i++)
            {
                var metric = _factory.CreateCounter(_metricNames[i], _help, new CounterConfiguration
                {
                    LabelNames = _labels
                });

                for (var repeat = 0; repeat < RepeatCount; repeat++)
                    metric.WithLabels(_labels).Inc();
            }
        }

        [Benchmark]
        public void Gauge_Many()
        {
            for (var i = 0; i < _metricCount; i++)
            {
                var metric = _factory.CreateGauge(_metricNames[i], _help, new GaugeConfiguration
                {
                    LabelNames = _labels
                });

                for (var repeat = 0; repeat < RepeatCount; repeat++)
                    metric.WithLabels(_labels).Inc();
            }
        }

        [Benchmark]
        public void Summary_Many()
        {
            for (var i = 0; i < _metricCount; i++)
            {
                var metric = _factory.CreateSummary(_metricNames[i], _help, new SummaryConfiguration
                {
                    LabelNames = _labels
                });

                for (var repeat = 0; repeat < RepeatCount; repeat++)
                    metric.WithLabels(_labels).Observe(123);
            }
        }

        [Benchmark]
        public void Histogram_Many()
        {
            for (var i = 0; i < _metricCount; i++)
            {
                var metric = _factory.CreateHistogram(_metricNames[i], _help, new HistogramConfiguration
                {
                    LabelNames = _labels
                });

                for (var repeat = 0; repeat < RepeatCount; repeat++)
                    metric.WithLabels(_labels).Observe(123);
            }
        }

        [Benchmark]
        public void Counter_Many_Duplicates()
        {
            for (var dupe = 0; dupe < _duplicateCount; dupe++)
                for (var i = 0; i < _metricCount; i++)
                {
                    var metric = _factory.CreateCounter(_metricNames[i], _help, new CounterConfiguration
                    {
                        LabelNames = _labels
                    });

                    for (var repeat = 0; repeat < RepeatCount; repeat++)
                        metric.WithLabels(_labels).Inc();
                }
        }

        [Benchmark]
        public void Gauge_Many_Duplicates()
        {
            for (var dupe = 0; dupe < _duplicateCount; dupe++)
                for (var i = 0; i < _metricCount; i++)
                {
                    var metric = _factory.CreateGauge(_metricNames[i], _help, new GaugeConfiguration
                    {
                        LabelNames = _labels
                    });

                    for (var repeat = 0; repeat < RepeatCount; repeat++)
                        metric.WithLabels(_labels).Inc();
                }
        }

        [Benchmark]
        public void Summary_Many_Duplicates()
        {
            for (var dupe = 0; dupe < _duplicateCount; dupe++)
                for (var i = 0; i < _metricCount; i++)
                {
                    var metric = _factory.CreateSummary(_metricNames[i], _help, new SummaryConfiguration
                    {
                        LabelNames = _labels
                    });

                    for (var repeat = 0; repeat < RepeatCount; repeat++)
                        metric.WithLabels(_labels).Observe(123);
                }
        }

        [Benchmark]
        public void Histogram_Many_Duplicates()
        {
            for (var dupe = 0; dupe < _duplicateCount; dupe++)
                for (var i = 0; i < _metricCount; i++)
                {
                    var metric = _factory.CreateHistogram(_metricNames[i], _help, new HistogramConfiguration
                    {
                        LabelNames = _labels
                    });

                    for (var repeat = 0; repeat < RepeatCount; repeat++)
                        metric.WithLabels(_labels).Observe(123);
                }
        }
    }
}
