using BenchmarkDotNet.Attributes;
using Prometheus;

namespace Benchmark.NetCore;

[MemoryDiagnoser]
public class StringSequenceBenchmarks
{
    private static readonly string[] Values3Array = ["aaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbb", "cccccccccccccc"];
    private static readonly ReadOnlyMemory<string> Values3Memory = new(Values3Array);

    private static readonly string[] Values3ArrayPart1 = ["aaaaaaaaaaaaaaaaa"];
    private static readonly string[] Values3ArrayPart2 = ["bbbbbbbbbbbbbb"];
    private static readonly string[] Values3ArrayPart3 = ["cccccccccccccc"];

    [Benchmark]
    public void Create_From3Array()
    {
        StringSequence.From(Values3Array);
    }

    [Benchmark]
    public void Create_From3Memory()
    {
        StringSequence.From(Values3Memory);
    }

    [Benchmark]
    public void Create_From3ArrayConcat()
    {
        var part1 = StringSequence.From(Values3ArrayPart1);
        var part2 = StringSequence.From(Values3ArrayPart2);
        var part3 = StringSequence.From(Values3ArrayPart3);

        part1.Concat(part2).Concat(part3);
    }

    private static readonly StringSequence FromValues3 = StringSequence.From(Values3Array);
    private static readonly StringSequence Other = StringSequence.From(new[] { "fooooooooooooooo", "baaaaaaaaaaaaar", "baaaaaaaaaaz" });

    [Benchmark]
    public void Contains_Positive()
    {
        FromValues3.Contains(Values3Array[2]);
    }

    [Benchmark]
    public void Contains_Negative()
    {
        FromValues3.Contains("a string that is not in there");
    }

    [Benchmark]
    public void Equals_Positive()
    {
        FromValues3.Equals(FromValues3);
    }

    [Benchmark]
    public void Equals_Negative()
    {
        FromValues3.Equals(Other);
    }

    [Benchmark]
    public void Concat_Empty()
    {
        FromValues3.Concat(StringSequence.Empty);
    }

    [Benchmark]
    public void Concat_ToEmpty()
    {
        StringSequence.Empty.Concat(FromValues3);
    }
}
