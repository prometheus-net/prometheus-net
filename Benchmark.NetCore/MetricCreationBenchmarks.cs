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
        private const int _metricCount = 1000;

        /// <summary>
        /// Some benchmarks try to register metrics that already exist.
        /// </summary>
        private const int _duplicateCount = 5;

        /// <summary>
        /// How many times we repeat acquiring and incrementing the same instance.
        /// </summary>
        [Params(1, 5)]
        public int RepeatCount { get; set; }

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
