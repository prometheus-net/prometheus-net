using BenchmarkDotNet.Attributes;
using Prometheus;

namespace Benchmark.NetCore;

public class GaugeBenchmarks
{
    private readonly CollectorRegistry _registry;
    private readonly MetricFactory _factory;
    private readonly Gauge _gauge;

    public GaugeBenchmarks()
    {
        _registry = Metrics.NewCustomRegistry();
        _factory = Metrics.WithCustomRegistry(_registry);

        _gauge = _factory.CreateGauge("gauge", "help text");
    }

    [Benchmark]
    public void SetToCurrenTimeUtc()
    {
        _gauge.SetToCurrentTimeUtc();
    }
}
