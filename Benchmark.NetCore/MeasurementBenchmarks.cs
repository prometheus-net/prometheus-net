using BenchmarkDotNet.Attributes;
using Prometheus;

namespace Benchmark.NetCore;

/// <summary>
/// We take a bunch of measurements of each type of metric and show the cost.
/// </summary>
/// <remarks>
/// Total measurements = MeasurementCount * ThreadCount
/// </remarks>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[InvocationCount(1)] // The implementation does not support multiple invocations.
[MinIterationCount(50), MaxIterationCount(200)] // Help stabilize measurements.
public class MeasurementBenchmarks
{
    public enum MetricType
    {
        Counter,
        Gauge,
        Histogram,
        Summary
    }

    [Params(200_000)]
    public int MeasurementCount { get; set; }

    [Params(1, 16)]
    public int ThreadCount { get; set; }

    [Params(MetricType.Counter, /*MetricType.Gauge,*/ MetricType.Histogram/*, MetricType.Summary*/)]
    public MetricType TargetMetricType { get; set; }

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

    private readonly Exemplar.LabelKey _traceIdKey = Exemplar.Key("trace_id");
    private readonly Exemplar.LabelKey _spanIdKey = Exemplar.Key("span_id");

    // We preallocate the exemplar values to avoid measuring the random()->string serialization as part of the benchmark.
    // What we care about measuring is the overhead of processing the exemplar, not of generating/serializing it.
    private readonly string _traceIdValue = "7f825eb926a90af6961ace5f9a239945";
    private readonly string _spanIdValue = "a77603af408a13ec";

    private Exemplar.LabelPair _traceIdLabel;
    private Exemplar.LabelPair _spanIdLabel;

    public MeasurementBenchmarks()
    {
        _registry = Metrics.NewCustomRegistry();
        _factory = Metrics.WithCustomRegistry(_registry);

        // We add a label to each, as labeled usage is the typical usage.
        var counterTemplate = _factory.CreateCounter("counter", "test counter", new[] { "label" });
        var gaugeTemplate = _factory.CreateGauge("gauge", "test gauge", new[] { "label" });
        var summaryTemplate = _factory.CreateSummary("summary", "test summary", new[] { "label" }, new SummaryConfiguration
        {
            Objectives = new QuantileEpsilonPair[]
            {
                new(0.9, 0.1),
                new(0.95, 0.01),
                new(0.99, 0.005)
            }
        });
        var histogramTemplate = _factory.CreateHistogram("histogram", "test histogram", new[] { "label" }, new HistogramConfiguration
        {
            // 1 ms to 32K ms, 16 buckets. Same as used in HTTP metrics by default.
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 16)
        });

        // We cache the children, as is typical usage.
        _counter = counterTemplate.WithLabels("label value");
        _gauge = gaugeTemplate.WithLabels("label value");
        _summary = summaryTemplate.WithLabels("label value");
        _histogram = histogramTemplate.WithLabels("label value");

        // We take a single measurement, to warm things up and avoid any first-call impact.
        _counter.Inc();
        _gauge.Set(1);
        _summary.Observe(1);
        _histogram.Observe(1);
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

    [IterationSetup]
    public void Setup()
    {
        _getExemplar = Exemplars switch
        {
            ExemplarMode.Auto => () => null,
            ExemplarMode.None => () => Exemplar.None,
            ExemplarMode.Provided => () =>Exemplar.From(_traceIdLabel, _spanIdLabel),
            _ => throw new NotImplementedException(),
        };

        // We reuse the same registry for each iteration, as this represents typical (warmed up) usage.

        _threadReadyToStart = new ManualResetEventSlim[ThreadCount];
        _startThreads = new ManualResetEventSlim();
        _threads = new Thread[ThreadCount];

        for (var i = 0; i < ThreadCount; i++)
        {
            _threadReadyToStart[i] = new();
            _threads[i] = new Thread(GetBenchmarkThreadEntryPoint());
            _threads[i].Name = $"Measurements #{i}";
            _threads[i].Start(i);
        }

        // Wait for all threads to get ready. We will give them the go signal in the actual benchmark method.
        foreach (var e in _threadReadyToStart)
            e.Wait();
    }

    private ParameterizedThreadStart GetBenchmarkThreadEntryPoint() => TargetMetricType switch
    {
        MetricType.Counter => MeasurementThreadCounter,
        MetricType.Gauge => MeasurementThreadGauge,
        MetricType.Histogram => MeasurementThreadHistogram,
        MetricType.Summary => MeasurementThreadSummary,
        _ => throw new NotSupportedException()
    };

    [IterationCleanup]
    public void Cleanup()
    {
        _startThreads.Dispose();

        foreach (var e in _threadReadyToStart)
            e.Dispose();
    }

    private ManualResetEventSlim[] _threadReadyToStart;
    private ManualResetEventSlim _startThreads;
    private Thread[] _threads;

    private void MeasurementThreadCounter(object state)
    {
        var threadIndex = (int)state;

        _threadReadyToStart[threadIndex].Set();
        _startThreads.Wait();

        for (var i = 0; i < MeasurementCount; i++)
        {
            _counter.Inc(_getExemplar());
        }
    }

    private void MeasurementThreadGauge(object state)
    {
        var threadIndex = (int)state;

        _threadReadyToStart[threadIndex].Set();
        _startThreads.Wait();

        for (var i = 0; i < MeasurementCount; i++)
        {
            _gauge.Set(i);
        }
    }

    private void MeasurementThreadHistogram(object state)
    {
        var threadIndex = (int)state;

        _threadReadyToStart[threadIndex].Set();
        _startThreads.Wait();

        for (var i = 0; i < MeasurementCount; i++)
        {
            _histogram.Observe(i, _getExemplar());
        }
    }

    private void MeasurementThreadSummary(object state)
    {
        var threadIndex = (int)state;

        _threadReadyToStart[threadIndex].Set();
        _startThreads.Wait();

        for (var i = 0; i < MeasurementCount; i++)
        {
            _summary.Observe(i);
        }
    }

    [Benchmark]
    public void MeasurementPerformance()
    {
        _startThreads.Set();

        for (var i = 0; i < _threads.Length; i++)
            _threads[i].Join();
    }

    private Exemplar GetExemplar() => Exemplars switch
    {
        ExemplarMode.Auto => null,
        ExemplarMode.None => Exemplar.None,
        ExemplarMode.Provided => Exemplar.From(_traceIdLabel, _spanIdLabel),
        _ => throw new NotImplementedException(),
    };

    private Func<Exemplar> _getExemplar;
}
