using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Prometheus;

namespace Benchmark.NetCore;

[MemoryDiagnoser]
//[EventPipeProfiler(EventPipeProfile.CpuSampling)]
public class ExemplarBenchmarks
{
    private readonly CollectorRegistry _registry;
    private readonly MetricFactory _factory;
    private readonly Counter _counter;

    public ExemplarBenchmarks()
    {
        _registry = Metrics.NewCustomRegistry();
        _factory = Metrics.WithCustomRegistry(_registry);

        // We provide the exemplars manually, without using the default behavior.
        _factory.ExemplarBehavior = ExemplarBehavior.NoExemplars();

        _counter = _factory.CreateCounter("gauge", "help text");
    }

    // Just establish a baseline - how much time/memory do we spend if not recording an exemplar.
    [Benchmark(Baseline = true)]
    public void Observe_NoExemplar()
    {
        _counter.Inc(123);
    }

    // Just as a sanity check, this should not cost us anything extra and may even be cheaper as we skip the default behavior lookup.
    [Benchmark]
    public void Observe_EmptyExemplar()
    {
        _counter.Inc(123, Exemplar.None);
    }

    private static readonly Exemplar.LabelKey CustomLabelKey1 = Exemplar.Key("my_key");
    private static readonly Exemplar.LabelKey CustomLabelKey2 = Exemplar.Key("my_key2");

    // A manually specified custom exemplar with some arbitrary value.
    [Benchmark]
    public void Observe_CustomExemplar()
    {
        _counter.Inc(123, Exemplar.From(CustomLabelKey1.WithValue("my_value"), CustomLabelKey2.WithValue("my_value2")));
    }

    // An exemplar extracted from the current trace context when there is no trace context.
    [Benchmark]
    public void Observe_ExemplarFromEmptyTraceContext()
    {
        _counter.Inc(123, Exemplar.FromTraceContext());
    }

    [GlobalSetup(Targets = new[] { nameof(Observe_ExemplarFromTraceContext) })]
    public void Setup_ExemplarFromTraceContext()
    {
        new Activity("test activity").Start();

        if (Activity.Current == null)
            throw new Exception("Sanity check failed.");
    }

    // An exemplar extracted from the current trace context when there is a trace context.
    [Benchmark]
    public void Observe_ExemplarFromTraceContext()
    {
        _counter.Inc(123, Exemplar.FromTraceContext());
    }

    [GlobalCleanup(Targets = new[] { nameof(Observe_ExemplarFromEmptyTraceContext), nameof(Observe_ExemplarFromTraceContext) })]
    public void Cleanup()
    {
        Activity.Current = null;
    }
}
