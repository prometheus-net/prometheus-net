using BenchmarkDotNet.Running;

namespace Benchmark
{
    public sealed class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<AsciiFormatterBenchmarks>();
            BenchmarkRunner.Run<LabelBenchmarks>();
            BenchmarkRunner.Run<MetricCreationBenchmarks>();
        }
    }
}
