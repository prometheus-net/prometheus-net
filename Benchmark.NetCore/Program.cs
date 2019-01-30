using BenchmarkDotNet.Running;

namespace Benchmark.NetCore
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            BenchmarkRunner.Run<MetricCreationBenchmarks>();
            BenchmarkRunner.Run<AsciiFormatterBenchmarks>();
            BenchmarkRunner.Run<LabelBenchmarks>();
            BenchmarkRunner.Run<HttpExporterBenchmarks>();
        }
    }
}
