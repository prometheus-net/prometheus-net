namespace Prometheus;

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
    ValueTask WriteFamilyDeclarationAsync(string name, byte[] nameBytes, byte[] helpBytes, MetricType type,
        byte[] typeBytes, CancellationToken cancel);

    /// <summary>
    /// Writes out a single metric point with a floating point value.
    /// </summary>
    ValueTask WriteMetricPointAsync(byte[] name, byte[] flattenedLabels, CanonicalLabel extraLabel,
        double value, ObservedExemplar exemplar, byte[]? suffix, CancellationToken cancel);

    /// <summary>
    /// Writes out a single metric point with an integer value.
    /// </summary>
    ValueTask WriteMetricPointAsync(byte[] name, byte[] flattenedLabels, CanonicalLabel extraLabel,
        long value, ObservedExemplar exemplar, byte[]? suffix, CancellationToken cancel);

    /// <summary>
    /// Writes out terminal lines
    /// </summary>
    ValueTask WriteEnd(CancellationToken cancel);

    /// <summary>
    /// Flushes any pending buffers. Always call this after all your write calls.
    /// </summary>
    Task FlushAsync(CancellationToken cancel);
}