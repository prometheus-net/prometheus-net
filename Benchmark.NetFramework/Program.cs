using BenchmarkDotNet.Running;

namespace Benchmark.NetFramework
{
    public sealed class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkRunner.Run<MetricCreationBenchmarks>();
            BenchmarkRunner.Run<AsciiFormatterBenchmarks>();
            BenchmarkRunner.Run<LabelBenchmarks>();
            BenchmarkRunner.Run<HttpExporterBenchmarks>();
        }
    }
}
