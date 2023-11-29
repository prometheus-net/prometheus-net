using BenchmarkDotNet.Attributes;
using Prometheus;
using SDM=System.Diagnostics.Metrics;

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
public class MeterAdapterBenchmarks
{
    public enum MetricType
    {
        CounterInt,
        CounterFloat,
        HistogramInt,
        HistogramFloat
    }

    [Params(200_000)]
    public int MeasurementCount { get; set; }

    [Params(1, 16)]
    public int ThreadCount { get; set; }

    [Params(MetricType.CounterInt, MetricType.CounterFloat, MetricType.HistogramInt, MetricType.HistogramFloat)]
    public MetricType TargetMetricType { get; set; }

    private readonly SDM.Meter _meter = new("prometheus-net benchmark");
    private readonly SDM.Counter<long> _intCounter;
    private readonly SDM.Counter<double> _floatCounter;
    private readonly SDM.Histogram<long> _intHistogram;
    private readonly SDM.Histogram<double> _floatHistogram;

    private readonly CollectorRegistry _registry;
    private readonly MetricFactory _factory;

    private readonly KeyValuePair<string, object> label = new("label", "value");


    public MeterAdapterBenchmarks()
    {
        _intCounter = _meter.CreateCounter<long>("int_counter");
        _floatCounter = _meter.CreateCounter<double>("float_counter");
        _intHistogram = _meter.CreateHistogram<long>("int_histogram");
        _floatHistogram = _meter.CreateHistogram<double>("float_histogram");

        _registry = Metrics.NewCustomRegistry();
        _factory = Metrics.WithCustomRegistry(_registry);

        MeterAdapter.StartListening(new MeterAdapterOptions {
            InstrumentFilterPredicate = instrument => instrument.Meter == _meter,
            Registry = _registry,
            MetricFactory = _factory,
            ResolveHistogramBuckets = instrument =>
                instrument == _floatHistogram || instrument == _intHistogram
                    ? Histogram.ExponentialBuckets(0.001, 2, 16)
                    : new double[1] { 1 },
        });

        // We take a single measurement, to warm things up and avoid any first-call impact.
        _intCounter.Add(1, label);
        _floatCounter.Add(1, label);
        _intHistogram.Record(1, label);
        _floatHistogram.Record(1, label);
    }

    [IterationSetup]
    public void Setup()
    {
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
        MetricType.CounterInt => MeasurementThreadIntCounter,
        MetricType.CounterFloat => MeasurementThreadFloatCounter,
        MetricType.HistogramInt => MeasurementThreadIntHistogram,
        MetricType.HistogramFloat => MeasurementThreadFloatHistogram,
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

    private void MeasurementThreadIntCounter(object state)
    {
        var threadIndex = (int)state;

        _threadReadyToStart[threadIndex].Set();
        _startThreads.Wait();

        for (var i = 0; i < MeasurementCount; i++)
        {
            _intCounter.Add(i, label);
        }
    }
    private void MeasurementThreadFloatCounter(object state)
    {
        var threadIndex = (int)state;

        _threadReadyToStart[threadIndex].Set();
        _startThreads.Wait();

        for (var i = 0; i < MeasurementCount; i++)
        {
            _floatCounter.Add(i, label);
        }
    }

    private void MeasurementThreadIntHistogram(object state)
    {
        var threadIndex = (int)state;

        _threadReadyToStart[threadIndex].Set();
        _startThreads.Wait();

        for (var i = 0; i < MeasurementCount; i++)
        {
            _intHistogram.Record(i, label);
        }
    }

    private void MeasurementThreadFloatHistogram(object state)
    {
        var threadIndex = (int)state;

        _threadReadyToStart[threadIndex].Set();
        _startThreads.Wait();

        for (var i = 0; i < MeasurementCount; i++)
        {
            _floatHistogram.Record(i, label);
        }
    }

    [Benchmark]
    public void MeasurementPerformance()
    {
        _startThreads.Set();

        for (var i = 0; i < _threads.Length; i++)
            _threads[i].Join();
    }
}
