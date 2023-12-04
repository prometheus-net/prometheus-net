using BenchmarkDotNet.Attributes;
using Prometheus;

namespace Benchmark.NetCore;

[MemoryDiagnoser]
public class StringSequenceBenchmarks
{
    private static readonly string[] Values3Array = ["aaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbb", "cccccccccccccc"];

    [Benchmark]
    public void Create_From3Array()
    {
        StringSequence.From(Values3Array);
    }
}
