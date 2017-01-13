using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: AssemblyProduct("prometheus-net")]
[assembly: AssemblyCopyright("Copyright © 2015 andrasm")]

[assembly: AssemblyVersion("1.3.4.0")]
[assembly: AssemblyFileVersion("1.3.4.0")]

[assembly: InternalsVisibleTo("prometheus-net.tests")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif
