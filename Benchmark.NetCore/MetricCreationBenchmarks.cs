using BenchmarkDotNet.Attributes;
using Prometheus;

namespace Benchmark.NetCore;

/// <summary>
/// One pattern advocated by Prometheus documentation is to implement scraping of external systems by
/// creating a brand new set of metrics for each scrape. So let's benchmark this scenario.
/// </summary>
[MemoryDiagnoser]
// This seems to need a lot of warmup to stabilize.
[WarmupCount(50)]
//[EventPipeProfiler(BenchmarkDotNet.Diagnosers.EventPipeProfile.GcVerbose)]
public class MetricCreationBenchmarks
{
    /// <summary>
    /// Just to ensure that a benchmark iteration has enough to do for stable and meaningful results.
    /// </summary>
    private const int _metricCount = 10_000;

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
    private IManagedLifetimeMetricFactory _managedLifetimeFactory;

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

        _managedLifetimeFactory = _factory.WithManagedLifetime(expiresAfter: TimeSpan.FromHours(1));
    }

    // We use the same strings both for the names and the values.
    private static readonly string[] _labels = ["foo", "bar", "baz"];

    private static readonly CounterConfiguration _counterConfiguration = CounterConfiguration.Default;
    private static readonly GaugeConfiguration _gaugeConfiguration = GaugeConfiguration.Default;
    private static readonly SummaryConfiguration _summaryConfiguration = SummaryConfiguration.Default;
    private static readonly HistogramConfiguration _histogramConfiguration = HistogramConfiguration.Default;

    [Benchmark]
    public void Counter_ArrayLabels()
    {
        for (var i = 0; i < _metricCount; i++)
        {
            var metric = _factory.CreateCounter(_metricNames[i], _help, _labels, _counterConfiguration);

            for (var repeat = 0; repeat < RepeatCount; repeat++)
                metric.WithLabels(_labels).Inc();
        }
    }

    [Benchmark]
    public void Counter_MemoryLabels()
    {
        // The overloads accepting string[] and ROM<string> are functionally equivalent, though any conversion adds some overhead.
        var labelsMemory = _labels.AsMemory();

        for (var i = 0; i < _metricCount; i++)
        {
            var metric = _factory.CreateCounter(_metricNames[i], _help, _labels, _counterConfiguration);

            for (var repeat = 0; repeat < RepeatCount; repeat++)
                metric.WithLabels(labelsMemory).Inc();
        }
    }

    [Benchmark]
    public void Counter_SpanLabels()
    {
        var labelsSpan = _labels.AsSpan();

        for (var i = 0; i < _metricCount; i++)
        {
            var metric = _factory.CreateCounter(_metricNames[i], _help, _labels, _counterConfiguration);

            for (var repeat = 0; repeat < RepeatCount; repeat++)
                // If code is aware that it is repeating the registration, using the Span overloads offers optimal performance.
                metric.WithLabels(labelsSpan).Inc();
        }
    }

    [Benchmark]
    public void Counter_ManagedLifetime()
    {
        // Typical usage for explicitly lifetime-managed metrics is to pass the label values as span, as they may already be known.
        var labelsSpan = _labels.AsSpan();

        for (var i = 0; i < _metricCount; i++)
        {
            var metric = _managedLifetimeFactory.CreateCounter(_metricNames[i], _help, _labels, _counterConfiguration);

            for (var repeat = 0; repeat < RepeatCount; repeat++)
                metric.WithLease(static x => x.Inc(), labelsSpan);
        }
    }

    [Benchmark]
    public void Counter_10Duplicates()
    {
        // We try to register the same metric 10 times, which is wasteful but shows us the overhead from doing this.

        for (var dupe = 0; dupe < 10; dupe++)
            for (var i = 0; i < _metricCount; i++)
            {
                var metric = _factory.CreateCounter(_metricNames[i], _help, _labels, _counterConfiguration);

                for (var repeat = 0; repeat < RepeatCount; repeat++)
                    // We do not use the Span overload here to exemplify "naive" code not aware of the repetition.
                    metric.WithLabels(_labels).Inc();
            }
    }

    [Benchmark]
    public void Gauge()
    {
        for (var i = 0; i < _metricCount; i++)
        {
            var metric = _factory.CreateGauge(_metricNames[i], _help, _labels, _gaugeConfiguration);

            for (var repeat = 0; repeat < RepeatCount; repeat++)
                // We do not use the Span overload here to exemplify "naive" code not aware of the repetition.
                metric.WithLabels(_labels).Set(repeat);
        }
    }

    // Disabled because it is slow and Summary is a legacy metric type that is not recommended for new usage.
    //[Benchmark]
    public void Summary()
    {
        for (var i = 0; i < _metricCount; i++)
        {
            var metric = _factory.CreateSummary(_metricNames[i], _help, _labels, _summaryConfiguration);

            for (var repeat = 0; repeat < RepeatCount; repeat++)
                // We do not use the Span overload here to exemplify "naive" code not aware of the repetition.
                metric.WithLabels(_labels).Observe(123);
        }
    }

    [Benchmark]
    public void Histogram()
    {
        for (var i = 0; i < _metricCount; i++)
        {
            var metric = _factory.CreateHistogram(_metricNames[i], _help, _labels, _histogramConfiguration);

            for (var repeat = 0; repeat < RepeatCount; repeat++)
                // We do not use the Span overload here to exemplify "naive" code not aware of the repetition.
                metric.WithLabels(_labels).Observe(123);
        }
    }
}
