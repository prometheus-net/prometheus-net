using Prometheus.DataContracts;
using System.Collections.Generic;

namespace Prometheus
{
    /// <summary>
    /// A collector mantains one or more metric families worth of data. Builtin collectors like Gaguge focus
    /// on a single metric family, whereas custom implementation are much more free in what they allow.
    /// </summary>
    public interface ICollector
    {
        /// <summary>
        /// The name of the collector. For builtin collectors, this is the name of the metric family
        /// but this need not be the case with custom collectors (as they may even return multiple families).
        /// 
        /// Only one collector with the same name can be registered in one collector registry.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Label keys applied by metrics using this collector.
        /// 
        /// This is used with builtin collectors to avoid causing conflicts when registering the same collector
        /// twice with different label names. It need not match the actual metric families in custom collectors.
        /// </summary>
        string[] LabelNames { get; }

        /// <summary>
        /// Collects one or more metric families' worth of data. Anything provided here
        /// is exported to Prometheus after some basic validity checking.
        /// </summary>
        IEnumerable<MetricFamily> Collect();
    }
}