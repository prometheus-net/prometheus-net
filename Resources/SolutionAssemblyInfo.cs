using System.Reflection;
using System.Runtime.CompilerServices;

// This is the real version number, used in NuGet packages and for display purposes.
[assembly: AssemblyFileVersion("2.2.0")]

// Only use major version here, with others kept at zero, for correct assembly binding logic.
[assembly: AssemblyVersion("2.2.0")]

[assembly: InternalsVisibleTo("Tests.NetFramework")]
[assembly: InternalsVisibleTo("Tests.NetCore")]
[assembly: InternalsVisibleTo("Benchmark.NetFramework")]