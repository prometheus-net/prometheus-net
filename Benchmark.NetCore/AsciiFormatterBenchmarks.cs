using BenchmarkDotNet.Attributes;
using Prometheus;
using System.IO;
using System.Linq;

namespace Benchmark.NetCore
{
    /// <summary>
    /// ASCII formatter is always used by Prometheus 2.0, so its performance is somewhat important.
    /// </summary>
    [MemoryDiagnoser]
    public class AsciiFormatterBenchmarks
    {
        // Metric -> Variant -> Label values
        private static readonly string[][][] _labelValueRows;

        private const int _metricCount = 10;
        private const int _variantCount = 10;
        private const int _labelCount = 5;

        private const string _help = "arbitrary help message for metric, not relevant for benchmarking";

        static AsciiFormatterBenchmarks()
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

        private readonly DefaultCollectorRegistry _registry = new DefaultCollectorRegistry();
        private readonly Counter[] _counters;
        private readonly Gauge[] _gauges;
        private readonly Summary[] _summaries;
        private readonly Histogram[] _histograms;

        public AsciiFormatterBenchmarks()
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
                _histograms[metricIndex] = factory.CreateHistogram($"histogram{metricIndex:D2}", _help, null, _labelValueRows[metricIndex][0]);
            }
        }

        private Prometheus.DataContracts.MetricFamily[] _data;
        private byte[] _outputBuffer;

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

            // Have to transform to array in order to materialize the iterator's results.
            _data = _registry.CollectAll().ToArray();

            // Use a preallocated fixed size buffer to prevent MemoryStream reallocations in benchmarks.
            _outputBuffer = new byte[32 * 1024 * 1024];
        }

        [Benchmark]
        public void Serialize()
        {
            using (var stream = new MemoryStream(_outputBuffer))
            {
                AsciiFormatter.Format(stream, _data);
            }
        }
    }
}
