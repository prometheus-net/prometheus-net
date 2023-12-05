using BenchmarkDotNet.Attributes;
using Prometheus;

namespace Benchmark.NetCore;

/// <summary>
/// Here we try to ensure that creating/using expiring metrics does not impose too heavy of a performance burden or create easily identifiable memory leaks.
/// </summary>
[MemoryDiagnoser]
// This seems to need a lot of warmup to stabilize.
[WarmupCount(50)]
// This seems to need a lot of iterations to stabilize.
[IterationCount(50)]
//[EventPipeProfiler(BenchmarkDotNet.Diagnosers.EventPipeProfile.GcVerbose)]
public class MetricExpirationBenchmarks
{
    /// <summary>
    /// Just to ensure that a benchmark iteration has enough to do for stable and meaningful results.
    /// </summary>
    private const int _metricCount = 25_000;

    /// <summary>
    /// If true, we preallocate a lifetime for every metric, so the benchmark only measures the actual usage
    /// of the metric and not the first-lease setup, as these two components of a metric lifetime can have different impact in different cases.
    /// </summary>
    [Params(true, false)]
    public bool PreallocateLifetime { get; set; }

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

    // We use the same strings both for the names and the values.
    private static readonly string[] _labels = ["foo", "bar", "baz"];

    private ManualDelayer _delayer;

    private readonly ManagedLifetimeMetricHandle<Counter.Child, ICounter>[] _counters = new ManagedLifetimeMetricHandle<Counter.Child, ICounter>[_metricCount];

    [IterationSetup]
    public void Setup()
    {
        _registry = Metrics.NewCustomRegistry();
        _factory = Metrics.WithCustomRegistry(_registry)
            // We enable lifetime management but set the expiration timer very high to avoid expiration in the middle of the benchmark.
            .WithManagedLifetime(expiresAfter: TimeSpan.FromHours(24));

        _delayer = new();

        for (var i = 0; i < _metricCount; i++)
        {
            var counter = CreateCounter(_metricNames[i], _help, _labels);
            _counters[i] = counter;

            // Both the usage and the lifetime allocation matter but we want to bring them out separately in the benchmarks.
            if (PreallocateLifetime)
                counter.AcquireRefLease(out _, _labels).Dispose();
        }
    }

    [IterationCleanup]
    public void Cleanup()
    {
        // Ensure that all metrics are marked as expired, so the expiration processing logic destroys them all.
        // This causes some extra work during cleanup but on the other hand, it ensures good isolation between iterations, so fine.
        foreach (var counter in _counters)
            counter.SetAllKeepaliveTimestampsToDistantPast();

        // Twice and with some sleep time, just for good measure.
        // BenchmarkDotNet today does not support async here, so we do a sleep to let the reaper thread process things.
        _delayer.BreakAllDelays();
        Thread.Sleep(millisecondsTimeout: 5);
    }

    private ManagedLifetimeMetricHandle<Counter.Child, ICounter> CreateCounter(string name, string help, string[] labels)
    {
        var counter = (ManagedLifetimeMetricHandle<Counter.Child, ICounter>)_factory.CreateCounter(name, help, labels);

        // We use a breakable delayer to ensure that we can control when the metric expiration logic runs, so one iteration
        // of the benchmark does not start to interfere with another iteration just because some timers are left running.
        counter.Delayer = _delayer;

        return counter;
    }

    [Benchmark]
    public void Use_AutoLease_Once()
    {
        for (var i = 0; i < _metricCount; i++)
        {
            var wrapper = _counters[i].WithExtendLifetimeOnUse();

            // Auto-leasing is used as a drop-in replacement in a context that is not aware the metric is lifetime-managed.
            // This means the typical usage is to pass a string[] (or ROM) and not a span (which would be a hint that it already exists).
            wrapper.WithLabels(_labels).Inc();
        }
    }

    [Benchmark]
    public void Use_AutoLease_With10Duplicates()
    {
        for (var dupe = 0; dupe < 10; dupe++)
            for (var i = 0; i < _metricCount; i++)
            {
                var wrapper = _counters[i].WithExtendLifetimeOnUse();

                // Auto-leasing is used as a drop-in replacement in a context that is not aware the metric is lifetime-managed.
                // This means the typical usage is to pass a string[] (or ROM) and not a span (which would be a hint that it already exists).
                wrapper.WithLabels(_labels).Inc();
            }
    }

    [Benchmark]
    public void Use_AutoLease_Once_With10Repeats()
    {
        for (var i = 0; i < _metricCount; i++)
        {
            var wrapper = _counters[i].WithExtendLifetimeOnUse();

            for (var repeat = 0; repeat < 10; repeat++)
                // Auto-leasing is used as a drop-in replacement in a context that is not aware the metric is lifetime-managed.
                // This means the typical usage is to pass a string[] (or ROM) and not a span (which would be a hint that it already exists).
                wrapper.WithLabels(_labels).Inc();
        }
    }

    [Benchmark(Baseline = true)]
    public void Use_ManualLease()
    {
        // Typical usage for explicitly lifetime-managed metrics is to pass the label values as span, as they may already be known.
        var labelValues = _labels.AsSpan();

        for (var i = 0; i < _metricCount; i++)
        {
            using var lease = _counters[i].AcquireLease(out var instance, labelValues);
            instance.Inc();
        }
    }

    [Benchmark]
    public void Use_ManualRefLease()
    {
        // Typical usage for explicitly lifetime-managed metrics is to pass the label values as span, as they may already be known.
        var labelValues = _labels.AsSpan();

        for (var i = 0; i < _metricCount; i++)
        {
            using var lease = _counters[i].AcquireRefLease(out var instance, labelValues);
            instance.Inc();
        }
    }

    private static void IncrementCounter(ICounter counter)
    {
        counter.Inc();
    }

    [Benchmark]
    public void Use_WithLease()
    {
        // Typical usage for explicitly lifetime-managed metrics is to pass the label values as span, as they may already be known.
        var labelValues = _labels.AsSpan();

        // Reuse the delegate.
        Action<ICounter> incrementCounterAction = IncrementCounter;

        for (var i = 0; i < _metricCount; i++)
            _counters[i].WithLease(incrementCounterAction, labelValues);
    }
}
