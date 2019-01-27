using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.CsProj;

namespace Benchmark.NetFramework
{
    public class MultipleRuntimes : ManualConfig
    {
        public MultipleRuntimes()
        {
            Add(Job.Default.With(Runtime.Core).With(CsProjCoreToolchain.NetCoreApp22));

            Add(Job.Default.With(Runtime.Clr).With(CsProjClassicNetToolchain.Net471));
        }
    }
}