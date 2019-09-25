using BenchmarkDotNet.Attributes;
using Prometheus;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Benchmark.NetCore
{
    /// <summary>
    /// Summary can be quite expensive to use due to its quantile measurement logic.
    /// This benchmark helps get a grip on the facts.
    /// </summary>
    [MemoryDiagnoser]
    public class SummaryBenchmarks
    {
        // Arbitrary but reasonable objectices we might use for a summary.
        private static readonly QuantileEpsilonPair[] Objectives = new[]
        {
            new QuantileEpsilonPair(0.5, 0.05),
            new QuantileEpsilonPair(0.9, 0.01),
            new QuantileEpsilonPair(0.95, 0.01),
            new QuantileEpsilonPair(0.99, 0.005)
        };

        // We pre-generate some random data that we feed into the benchmark, to avoid measuring data generation.
        private static readonly double[] Values = new double[1 * 1024 * 1024];

        private static readonly TimeSpan ExportInterval = TimeSpan.FromMinutes(1);

        static SummaryBenchmarks()
        {
            var rnd = new Random();

            for (var i = 0; i < Values.Length; i++)
                Values[i] = rnd.NextDouble();
        }

        private CollectorRegistry _registry;
        private MetricFactory _factory;

        [IterationSetup]
        public void Setup()
        {
            _registry = Metrics.NewCustomRegistry();
            _factory = Metrics.WithCustomRegistry(_registry);
        }

        [Params(1, 10, 100, 1000, 10000)]
        public int MeasurementsPerSecond { get; set; }

        [Benchmark]
        public async Task Summary_NPerSecond_For10Minutes()
        {
            var summary = _factory.CreateSummary("metric_name", "help_string", new SummaryConfiguration
            {
                Objectives = Objectives
            });

            var now = DateTime.UtcNow;

            // We start far enough back to cover the entire age range of the summary.
            var t = now - Summary.DefMaxAge;
            var lastExport = t;

            while (t < now)
            {
                for (var i = 0; i < MeasurementsPerSecond; i++)
                    summary.Observe(Values[i % Values.Length]);

                t += TimeSpan.FromSeconds(1);

                if (lastExport + ExportInterval <= t)
                {
                    lastExport = t;

                    await summary.CollectAndSerializeAsync(new TextSerializer(Stream.Null), default);
                }
            }
        }
    }
}
