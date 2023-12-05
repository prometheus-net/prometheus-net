using BenchmarkDotNet.Attributes;
using Prometheus;
using SDM = System.Diagnostics.Metrics;

namespace Benchmark.NetCore;

/// <summary>
/// Equivalent of MeasurementBenchmarks, except we publish the data via .NET Meters API and convert via MeterAdapter.
/// </summary>
[MemoryDiagnoser]
//[EventPipeProfiler(BenchmarkDotNet.Diagnosers.EventPipeProfile.GcVerbose)]
public class MeterAdapterBenchmarks
{
    [Params(100_000)]
    public int MeasurementCount { get; set; }

    private readonly SDM.Meter _meter = new("prometheus-net benchmark");
    private readonly SDM.Counter<long> _intCounter;
    private readonly SDM.Counter<double> _floatCounter;
    private readonly SDM.Histogram<long> _intHistogram;
    private readonly SDM.Histogram<double> _floatHistogram;

    private readonly CollectorRegistry _registry;

    private readonly IDisposable _meterAdapter;

    private readonly KeyValuePair<string, object> _label = new("label", "label value");

    public MeterAdapterBenchmarks()
    {
        _intCounter = _meter.CreateCounter<long>("int_counter", description: "This is an integer counter.");
        _floatCounter = _meter.CreateCounter<double>("float_counter", description: "This is a floating-point counter.");
        _intHistogram = _meter.CreateHistogram<long>("int_histogram", description: "This is an integer histogram.");
        _floatHistogram = _meter.CreateHistogram<double>("float_histogram", description: "This is a floating-point histogram.");

        _registry = Metrics.NewCustomRegistry();

        _meterAdapter = MeterAdapter.StartListening(new MeterAdapterOptions
        {
            InstrumentFilterPredicate = instrument => instrument.Meter == _meter,
            Registry = _registry,
            // We resolve a custom set of buckets here, for maximum stress and to avoid easily reused default buckets.
            // 1 ms to 32K ms, 16 buckets. Same as used in HTTP metrics by default.
            ResolveHistogramBuckets = _ => Histogram.ExponentialBuckets(0.001, 2, 16)
        });

        // We take a single measurement, to warm things up and avoid any first-call impact.
        _intCounter.Add(1, _label);
        _floatCounter.Add(1, _label);
        _intHistogram.Record(1, _label);
        _floatHistogram.Record(1, _label);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _meterAdapter.Dispose();
    }

    [Benchmark]
    public void CounterInt()
    {
        for (var i = 0; i < MeasurementCount; i++)
        {
            _intCounter.Add(1, _label);
        }
    }

    [Benchmark]
    public void CounterFloat()
    {
        for (var i = 0; i < MeasurementCount; i++)
        {
            _floatCounter.Add(1, _label);
        }
    }

    [Benchmark]
    public void HistogramInt()
    {
        for (var i = 0; i < MeasurementCount; i++)
        {
            _intHistogram.Record(i, _label);
        }
    }

    [Benchmark]
    public void HistogramFloat()
    {
        for (var i = 0; i < MeasurementCount; i++)
        {
            _floatHistogram.Record(i, _label);
        }
    }
}
