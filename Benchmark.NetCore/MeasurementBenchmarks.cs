using BenchmarkDotNet.Attributes;
using Prometheus;

namespace Benchmark.NetCore;

/// <summary>
/// We take a bunch of measurements of each type of metric and show the cost.
/// </summary>
[MemoryDiagnoser]
public class MeasurementBenchmarks
{
    [Params(100_000)]
    public int MeasurementCount { get; set; }

    [Params(ExemplarMode.Auto, ExemplarMode.None, ExemplarMode.Provided)]
    public ExemplarMode Exemplars { get; set; }

    public enum ExemplarMode
    {
        /// <summary>
        /// No user-supplied exemplar but the default behavior is allowed to execute (and fail to provide an exemplar).
        /// </summary>
        Auto,

        /// <summary>
        /// Explicitly indicating that no exemplar is to be used.
        /// </summary>
        None,

        /// <summary>
        /// Explicitly providing an exemplar.
        /// </summary>
        Provided
    }

    private readonly CollectorRegistry _registry;
    private readonly MetricFactory _factory;

    private readonly Counter.Child _counter;
    private readonly Gauge.Child _gauge;
    private readonly Summary.Child _summary;
    private readonly Histogram.Child _histogram;
    private readonly Histogram.Child _wideHistogram;

    private readonly Exemplar.LabelKey _traceIdKey = Exemplar.Key("trace_id");
    private readonly Exemplar.LabelKey _spanIdKey = Exemplar.Key("span_id");

    // We preallocate the exemplar values to avoid measuring the random()->string serialization as part of the benchmark.
    // What we care about measuring is the overhead of processing the exemplar, not of generating/serializing it.
    private readonly string _traceIdValue = "7f825eb926a90af6961ace5f9a239945";
    private readonly string _spanIdValue = "a77603af408a13ec";

    private Exemplar.LabelPair _traceIdLabel;
    private Exemplar.LabelPair _spanIdLabel;

    /// <summary>
    /// The max value we observe for histograms, to give us coverage of all the histogram buckets
    /// but not waste 90% of the benchmark on incrementing the +Inf bucket.
    /// </summary>
    private const int WideHistogramMaxValue = 32 * 1024;

    // Same but for the regular histogram.
    private readonly int _regularHistogramMaxValue;

    private static readonly string[] labelNames = ["label"];

    public MeasurementBenchmarks()
    {
        _registry = Metrics.NewCustomRegistry();
        _factory = Metrics.WithCustomRegistry(_registry);

        // We add a label to each, as labeled usage is the typical usage.
        var counterTemplate = _factory.CreateCounter("counter", "test counter", labelNames);
        var gaugeTemplate = _factory.CreateGauge("gauge", "test gauge", labelNames);
        var summaryTemplate = _factory.CreateSummary("summary", "test summary", labelNames, new SummaryConfiguration
        {
            Objectives = new QuantileEpsilonPair[]
            {
                new(0.9, 0.1),
                new(0.95, 0.01),
                new(0.99, 0.005)
            }
        });

        // 1 ms to 32K ms, 16 buckets. Same as used in HTTP metrics by default.
        var regularHistogramBuckets = Prometheus.Histogram.ExponentialBuckets(0.001, 2, 16);
        
        // Last one is +inf, so take the second-to-last.
        _regularHistogramMaxValue = (int)regularHistogramBuckets[^2];

        var histogramTemplate = _factory.CreateHistogram("histogram", "test histogram", labelNames, new HistogramConfiguration
        {
            Buckets = regularHistogramBuckets
        });

        var wideHistogramTemplate = _factory.CreateHistogram("wide_histogram", "test histogram", labelNames, new HistogramConfiguration
        {
            Buckets = Prometheus.Histogram.LinearBuckets(1, WideHistogramMaxValue / 128, 128)
        });

        // We cache the children, as is typical usage.
        _counter = counterTemplate.WithLabels("label value");
        _gauge = gaugeTemplate.WithLabels("label value");
        _summary = summaryTemplate.WithLabels("label value");
        _histogram = histogramTemplate.WithLabels("label value");
        _wideHistogram = wideHistogramTemplate.WithLabels("label value");

        // We take a single measurement, to warm things up and avoid any first-call impact.
        _counter.Inc();
        _gauge.Set(1);
        _summary.Observe(1);
        _histogram.Observe(1);
        _wideHistogram.Observe(1);
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        // There is an unavoidable string->bytes encoding overhead from this.
        // As it is fixed overhead based on user data size, we pre-encode the strings here to avoid them influencing the benchmark results.
        // We only preallocate the strings, however (creating the LabelPairs). We still do as much of the exemplar "processing" inline as feasible, to be realistic.
        _traceIdLabel = _traceIdKey.WithValue(_traceIdValue);
        _spanIdLabel = _spanIdKey.WithValue(_spanIdValue);
    }

    [Benchmark]
    public void Counter()
    {
        var exemplarProvider = GetExemplarProvider();

        for (var i = 0; i < MeasurementCount; i++)
        {
            _counter.Inc(exemplarProvider());
        }
    }

    [Benchmark]
    public void Gauge()
    {
        for (var i = 0; i < MeasurementCount; i++)
        {
            _gauge.Set(i);
        }
    }

    [Benchmark]
    public void Histogram()
    {
        var exemplarProvider = GetExemplarProvider();

        for (var i = 0; i < MeasurementCount; i++)
        {
            var value = i % _regularHistogramMaxValue;
            _histogram.Observe(value, exemplarProvider());
        }
    }

    [Benchmark]
    public void WideHistogram()
    {
        var exemplarProvider = GetExemplarProvider();

        for (var i = 0; i < MeasurementCount; i++)
        {
            var value = i % WideHistogramMaxValue;
            _wideHistogram.Observe(value, exemplarProvider());
        }
    }

    // Disabled because it is slow and Summary is a legacy metric type that is not recommended for new usage.
    //[Benchmark]
    public void Summary()
    {
        for (var i = 0; i < MeasurementCount; i++)
        {
            _summary.Observe(i);
        }
    }

    private Func<Exemplar> GetExemplarProvider() => Exemplars switch
    {
        ExemplarMode.Auto => () => null,
        ExemplarMode.None => () => Exemplar.None,
        ExemplarMode.Provided => () => Exemplar.From(_traceIdLabel, _spanIdLabel),
        _ => throw new NotImplementedException(),
    };
}
