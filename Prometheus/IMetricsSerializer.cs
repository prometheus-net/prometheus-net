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
        /// Writes the second part of the metric, the value (and the exemplar). Terminates with a newline 
        /// </summary>
        Task WriteValuePartAsync(double value, CancellationToken cancel);
        
        /// <summary>
        /// Writes the identifier for a series. Terminates with a SPACE. 
        /// </summary>
        Task WriteIdentifierPartAsync(ChildBase metric, CancellationToken cancellationToken, 
            string? postfix = null, string? extraLabelName = null, string? extraLabelValue = null);
        
        /// <summary>
        /// Flushes any pending buffers. Always call this after all your write calls.
        /// </summary>
        Task FlushAsync(CancellationToken cancel);
    }
}
