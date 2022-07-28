using BenchmarkDotNet.Attributes;
using Prometheus;

namespace Benchmark.NetCore
{
    [MemoryDiagnoser]
    public class LabelBenchmarks
    {
        // Metric -> Variant -> Label values
        private static readonly string[][][] _labelValueRows;

        private const int _metricCount = 10;
        private const int _variantCount = 10;
        private const int _labelCount = 5;

        static LabelBenchmarks()
        {
            _labelValueRows = new string[_metricCount][][];

            for (var metricIndex = 0; metricIndex < _metricCount; metricIndex++)
            {
                var variants = new string[_variantCount][];
                _labelValueRows[metricIndex] = variants;

                for (var variantIndex = 0; variantIndex < _variantCount; variantIndex++)
                {
                    var values = new string[_labelCount];
                    _labelValueRows[metricIndex][variantIndex] = values;

                    for (var labelIndex = 0; labelIndex < _labelCount; labelIndex++)
                        values[labelIndex] = $"metric{metricIndex:D2}_label{labelIndex:D2}_variant{variantIndex:D2}";
                }
            }
        }

        private readonly CollectorRegistry _registry = Metrics.NewCustomRegistry();
        private readonly Counter[] _metrics;

        public LabelBenchmarks()
        {
            _metrics = new Counter[_metricCount];

            var factory = Metrics.WithCustomRegistry(_registry);

            // Just use 1st variant for the keys (all we care about are that there is some name-like value in there).
            for (var metricIndex = 0; metricIndex < _metricCount; metricIndex++)
                _metrics[metricIndex] = factory.CreateCounter($"metric{metricIndex:D2}", "", _labelValueRows[metricIndex][0]);
        }

        /// <summary>
        /// Increments an unlabelled Collector instance for a single metric.
        /// </summary>
        [Benchmark]
        public void WithoutLabels_OneMetric_OneSeries()
        {
            _metrics[0].Inc();
        }

        /// <summary>
        /// Increments unlabelled Collector instances for a multiple metrics.
        /// </summary>
        [Benchmark]
        public void WithoutLabels_ManyMetrics_OneSeries()
        {
            for (var metricIndex = 0; metricIndex < _metricCount; metricIndex++)
                _metrics[metricIndex].Inc();
        }

        /// <summary>
        /// Increments a labelled Collector.Child instance for a single metric.
        /// </summary>
        [Benchmark]
        public void WithLabels_OneMetric_OneSeries()
        {
            _metrics[0].Labels(_labelValueRows[0][0]).Inc();
        }

        /// <summary>
        /// Increments labelled Collector.Child instances for one metric with multiple different sets of labels.
        /// </summary>
        [Benchmark]
        public void WithLabels_OneMetric_ManySeries()
        {
            for (var variantIndex = 0; variantIndex < _variantCount; variantIndex++)
                _metrics[0].Labels(_labelValueRows[0][variantIndex]).Inc();
        }

        /// <summary>
        /// Increments a labelled Collector.Child instance for multiple metrics.
        /// </summary>
        [Benchmark]
        public void WithLabels_ManyMetrics_OneSeries()
        {
            for (var metricIndex = 0; metricIndex < _metricCount; metricIndex++)
                _metrics[metricIndex].Labels(_labelValueRows[metricIndex][0]).Inc();
        }

        /// <summary>
        /// Increments labelled Collector.Child instances for multiple metrics with multiple different sets of labels.
        /// </summary>
        [Benchmark]
        public void WithLabels_ManyMetrics_ManySeries()
        {
            for (var metricIndex = 0; metricIndex < _metricCount; metricIndex++)
                for (var variantIndex = 0; variantIndex < _variantCount; variantIndex++)
                    _metrics[metricIndex].Labels(_labelValueRows[metricIndex][variantIndex]).Inc();
        }
    }
}
