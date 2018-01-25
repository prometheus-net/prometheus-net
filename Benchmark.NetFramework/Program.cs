using BenchmarkDotNet.Running;

namespace Benchmark
{
    public sealed class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<LabelBenchmarks>();
        }
    }
}
