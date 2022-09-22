using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prometheus
{
    /// <summary>
    /// Child-type-specific interface implemented by all collectors, used to enable substitution in test code.
    /// </summary>
    public interface ICollector<out TChild> : ICollector
        where TChild : ICollectorChild
    {
        TChild Unlabelled { get; }
        TChild WithLabels(params string[] labelValues);
    }

    /// <summary>
    /// Interface implemented by all collectors, used to enable substitution in test code.
    /// </summary>
    public interface ICollector
    {
        string Name { get; }
        string Help { get; }
        string[] LabelNames { get; }
    }
}
