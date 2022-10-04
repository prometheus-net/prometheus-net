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
    private const int _metricCount = 100;

    /// <summary>
    /// Some benchmarks try to register metrics that already exist.
    /// </summary>
    private const int _duplicateCount = 5;

    /// <summary>
    /// How many times we repeat acquiring and incrementing the same instance.
    /// </summary>
    [Params(1, 10)]
    public int RepeatCount { get; set; }

    /// <summary>
    /// If true, we preallocate a lifetime manager for every metric, so the benchmark only measures the actual usage
    /// of the metric and not the creation, as these two components of a metric lifetime can have different impact in different cases.
    /// </summary>
    [Params(true, false)]
    public bool PreallocateLifetimeManager { get; set; }

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

        var regularFactory = Metrics.WithCustomRegistry(_registry);

        // We create non-expiring versions of the metrics to pre-warm the metrics registry.
        // While not a realistic use case, it does help narrow down the benchmark workload to the actual part that is special about expiring metrics,
        // which means we get more useful results from this (rather than with some metric allocation overhead mixed in).
        for (var i = 0; i < _metricCount; i++)
        {
            var counter = regularFactory.CreateCounter(_metricNames[i], _help, _labels);
            counter.WithLabels(_labels);

            // If the params say so, we even preallocate the lifetime manager to ensure that we only measure the usage of the metric.
            // Both the usage and the lifetime manager allocation matter but we want to bring them out separately in the benchmarks.
            if (PreallocateLifetimeManager)
            {
                var managedLifetimeCounter = _factory.CreateCounter(_metricNames[i], _help, _labels);

                // And also take the first lease to pre-warm the lifetime manager.
                managedLifetimeCounter.AcquireLease(out _, _labels).Dispose();
            }
        }
    }

    // We use the same strings both for the names and the values.
    private static readonly string[] _labels = new[] { "foo", "bar", "baz" };

    [Benchmark]
    public void CreateAndUse_AutoLease()
    {
        for (var i = 0; i < _metricCount; i++)
        {
            var metric = _factory.CreateCounter(_metricNames[i], _help, _labels).WithExtendLifetimeOnUse();

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
                var metric = _factory.CreateCounter(_metricNames[i], _help, _labels).WithExtendLifetimeOnUse();

                for (var repeat = 0; repeat < RepeatCount; repeat++)
                    metric.WithLabels(_labels).Inc();
            }
    }

    [Benchmark(Baseline = true)]
    public void CreateAndUse_ManualLease()
    {
        for (var i = 0; i < _metricCount; i++)
        {
            var counter = _factory.CreateCounter(_metricNames[i], _help, _labels);

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
                var counter = _factory.CreateCounter(_metricNames[i], _help, _labels);

                for (var repeat = 0; repeat < RepeatCount; repeat++)
                {
                    using var lease = counter.AcquireLease(out var instance, _labels);
                    instance.Inc();
                }
            }
    }

    private static void IncrementCounter(ICounter counter)
    {
        counter.Inc();
    }

    [Benchmark]
    public void CreateAndUse_WithLease()
    {
        // Reuse the delegate.
        Action<ICounter> incrementCounterAction = IncrementCounter;

        for (var i = 0; i < _metricCount; i++)
        {
            var counter = _factory.CreateCounter(_metricNames[i], _help, _labels);

            for (var repeat = 0; repeat < RepeatCount; repeat++)
            {
                counter.WithLease(incrementCounterAction, _labels);
            }
        }
    }

    [Benchmark]
    public void CreateAndUse_WithLease_WithDuplicates()
    {
        // Reuse the delegate.
        Action<ICounter> incrementCounterAction = IncrementCounter;

        for (var dupe = 0; dupe < _duplicateCount; dupe++)
            for (var i = 0; i < _metricCount; i++)
            {
                var counter = _factory.CreateCounter(_metricNames[i], _help, _labels);

                for (var repeat = 0; repeat < RepeatCount; repeat++)
                    counter.WithLease(incrementCounterAction, _labels);
            }
    }
}
