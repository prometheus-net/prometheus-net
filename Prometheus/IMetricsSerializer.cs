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
        /// Writes out a single metric point
        /// </summary>
        /// <returns></returns>
        Task WriteMetricPointAsync(byte[] name, byte[] flattenedLabels, CanonicalLabel canonicalLabel,
            CancellationToken cancel, double value, ObservedExemplar exemplar, byte[]? suffix = null);

        /// <summary>
        /// Writes out terminal lines
        /// </summary>
        Task WriteEnd(CancellationToken cancel);
        
        /// <summary>
        /// Flushes any pending buffers. Always call this after all your write calls.
        /// </summary>
        Task FlushAsync(CancellationToken cancel);
    }
}