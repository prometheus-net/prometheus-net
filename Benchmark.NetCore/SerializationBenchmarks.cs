using BenchmarkDotNet.Attributes;
using Prometheus;
using System.IO;
using System.Threading.Tasks;

namespace Benchmark.NetCore
{
    [MemoryDiagnoser]
    public class SerializationBenchmarks
    {
        // Metric -> Variant -> Label values
        private static readonly string[][][] _labelValueRows;

        private const int _metricCount = 100;
        private const int _variantCount = 100;
        private const int _labelCount = 5;

        private const string _help = "arbitrary help message for metric, not relevant for benchmarking";

        static SerializationBenchmarks()
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
        private readonly Counter[] _counters;
        private readonly Gauge[] _gauges;
        private readonly Summary[] _summaries;
        private readonly Histogram[] _histograms;

        public SerializationBenchmarks()
        {
            _counters = new Counter[_metricCount];
            _gauges = new Gauge[_metricCount];
            _summaries = new Summary[_metricCount];
            _histograms = new Histogram[_metricCount];

            var factory = Metrics.WithCustomRegistry(_registry);

            // Just use 1st variant for the keys (all we care about are that there is some name-like value in there).
            for (var metricIndex = 0; metricIndex < _metricCount; metricIndex++)
            {
                _counters[metricIndex] = factory.CreateCounter($"counter{metricIndex:D2}", _help, _labelValueRows[metricIndex][0]);
                _gauges[metricIndex] = factory.CreateGauge($"gauge{metricIndex:D2}", _help, _labelValueRows[metricIndex][0]);
                _summaries[metricIndex] = factory.CreateSummary($"summary{metricIndex:D2}", _help, _labelValueRows[metricIndex][0]);
                _histograms[metricIndex] = factory.CreateHistogram($"histogram{metricIndex:D2}", _help, _labelValueRows[metricIndex][0]);
            }
        }

        [GlobalSetup]
        public void GenerateData()
        {
            for (var metricIndex = 0; metricIndex < _metricCount; metricIndex++)
                for (var variantIndex = 0; variantIndex < _variantCount; variantIndex++)
                {
                    _counters[metricIndex].Labels(_labelValueRows[metricIndex][variantIndex]).Inc();
                    _gauges[metricIndex].Labels(_labelValueRows[metricIndex][variantIndex]).Inc();
                    _summaries[metricIndex].Labels(_labelValueRows[metricIndex][variantIndex]).Observe(variantIndex);
                    _histograms[metricIndex].Labels(_labelValueRows[metricIndex][variantIndex]).Observe(variantIndex);
                }
        }

        [Benchmark]
        public async Task CollectAndSerialize()
        {
            await _registry.CollectAndSerializeAsync(new TextSerializer(Stream.Null), default);
        }
    }
}
