using BenchmarkDotNet.Running;

namespace Benchmark.NetCore
{
    public sealed class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkRunner.Run<HttpExporterBenchmarks>();
        }
    }
}
