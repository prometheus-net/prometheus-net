using BenchmarkDotNet.Running;

namespace Benchmark.NetCore;

internal class Program
{
    private static void Main(string[] args)
    {
        // Give user possibility to choose which benchmark to run.
        // Can be overridden from the command line with the --filter option.
        new BenchmarkSwitcher(typeof(Program).Assembly).Run(args);
    }
}
