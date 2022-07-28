using System.Threading;
using System.Threading.Tasks;

namespace Prometheus
{
    /// <summary>
    /// The only purpose this serves is to warn the developer when he might be accidentally introducing
    /// new serialization-time relationships. The serialization code is very tied to the text format and
    /// not intended to be a generic serialization mechanism.
    /// </summary>
    internal interface IMetricsSerializer
    {
        /// <summary>
        /// Writes the lines that declare the metric family.
        /// </summary>
        Task WriteFamilyDeclarationAsync(byte[][] headerLines, CancellationToken cancel);

        /// <summary>
        /// Writes a single metric in a metric family.
        /// </summary>
        Task WriteMetricAsync(byte[] identifier, double value, CancellationToken cancel);

        /// <summary>
        /// Flushes any pending buffers. Always call this after all your write calls.
        /// </summary>
        Task FlushAsync(CancellationToken cancel);
    }
}
