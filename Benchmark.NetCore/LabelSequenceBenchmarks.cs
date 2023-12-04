using BenchmarkDotNet.Attributes;
using Prometheus;

namespace Benchmark.NetCore;

[MemoryDiagnoser]
public class LabelSequenceBenchmarks
{
    private static readonly StringSequence Names3Array = StringSequence.From(new[] { "aaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbb", "cccccccccccccc" });
    private static readonly StringSequence Values3Array = StringSequence.From(new[] { "valueaaaaaaaaaaaaaaaaa", "valuebbbbbbbbbbbbbb", "valuecccccccccccccc" });

    [Benchmark]
    public void Create_From3Array()
    {
        LabelSequence.From(Names3Array, Values3Array);
    }
}
