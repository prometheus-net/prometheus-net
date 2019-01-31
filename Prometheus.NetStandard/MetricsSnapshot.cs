using System;
using System.Collections.Generic;
using System.IO;

namespace Prometheus
{
    /// <summary>
    /// A metrics snapshot that contains metrics from all collectors registered in a collector registry.
    /// </summary>
    public sealed class MetricsSnapshot
    {
        internal MetricsSnapshot(IList<MetricFamilyData> families)
        {
            Families = families ?? throw new ArgumentNullException(nameof(families));
        }

        internal IList<MetricFamilyData> Families { get; }

        /// <summary>
        /// Serializes the metrics to the provided stream.
        /// </summary>
        public void Serialize(Stream stream)
        {
            AsciiFormatter.Format(stream, Families);
        }
    }
}
