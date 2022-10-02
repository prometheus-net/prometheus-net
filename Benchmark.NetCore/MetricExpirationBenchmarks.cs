using BenchmarkDotNet.Attributes;
using Prometheus;

namespace Benchmark.NetCore;

/// <summary>
/// Here we try to ensure that creating/using expiring metrics does not impose too heavy of a performance burden or create easily identifiable memory leaks.
/// </summary>
[MemoryDiagnoser]
public class MetricExpirationBenchmarks
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

    static MetricExpirationBenchmarks()
    {
        _metricNames = new string[_metricCount];

        for (var i = 0; i < _metricCount; i++)
            _metricNames[i] = $"metric_{i:D4}";
    }

    private CollectorRegistry _registry;
    private IManagedLifetimeMetricFactory _factory;

    [IterationSetup]
    public void Setup()
    {
        _registry = Metrics.NewCustomRegistry();
        _factory = Metrics.WithCustomRegistry(_registry)
            // We enable lifetime management but set the expiration timer very high to avoid expiration in the middle of the benchmark.
            .WithManagedLifetime(expiresAfter: TimeSpan.FromHours(24));
    }

    // We use the same strings both for the names and the values.
    private static readonly string[] _labels = new[] { "foo", "bar", "baz" };

    // We cache this to focus the benchmarks on the part that is actually unique to expiring metrics.
    private static readonly CounterConfiguration _configuration = new CounterConfiguration
    {
        LabelNames = _labels
    };

    [Benchmark]
    public void CreateAndUse_AutoLease()
    {
        for (var i = 0; i < _metricCount; i++)
        {
            var metric = _factory.CreateCounter(_metricNames[i], _help, _configuration).WithExtendLifetimeOnUse();

            for (var repeat = 0; repeat < RepeatCount; repeat++)
                metric.WithLabels(_labels).Inc();
        }
    }

    [Benchmark]
    public void CreateAndUse_AutoLease_WithDuplicates()
    {
        for (var dupe = 0; dupe < _duplicateCount; dupe++)
            for (var i = 0; i < _metricCount; i++)
            {
                var metric = _factory.CreateCounter(_metricNames[i], _help, _configuration).WithExtendLifetimeOnUse();

                for (var repeat = 0; repeat < RepeatCount; repeat++)
                    metric.WithLabels(_labels).Inc();
            }
    }

    [Benchmark]
    public void CreateAndUse_ManualLease()
    {
        for (var i = 0; i < _metricCount; i++)
        {
            var counter = _factory.CreateCounter(_metricNames[i], _help, _configuration);

            for (var repeat = 0; repeat < RepeatCount; repeat++)
            {
                using var lease = counter.AcquireLease(out var instance, _labels);
                instance.Inc();
            }
        }
    }

    [Benchmark]
    public void CreateAndUse_ManualLease_WithDuplicates()
    {
        for (var dupe = 0; dupe < _duplicateCount; dupe++)
            for (var i = 0; i < _metricCount; i++)
            {
                var counter = _factory.CreateCounter(_metricNames[i], _help, _configuration);

                for (var repeat = 0; repeat < RepeatCount; repeat++)
                {
                    using var lease = counter.AcquireLease(out var instance, _labels);
                    instance.Inc();
                }
            }
    }
}
