using BenchmarkDotNet.Attributes;
using Prometheus;

namespace Benchmark.NetCore;

public class CounterBenchmarks
{
    private readonly CollectorRegistry _registry;
    private readonly MetricFactory _factory;
    private readonly Counter _counter;

    public CounterBenchmarks()
    {
        _registry = Metrics.NewCustomRegistry();
        _factory = Metrics.WithCustomRegistry(_registry);

        _counter = _factory.CreateCounter("gauge", "help text");
    }

    [Benchmark]
    public void IncToCurrentTimeUtc()
    {
        _counter.IncToCurrentTimeUtc();
    }
}
