using System.Buffers;

namespace Prometheus;

/// <summary>
/// A fully-formed exemplar, defined from a set of label name-value pairs. Create via Exemplar.From().
/// 
/// One-time use only - when you pass this value to a prometheus-net method, it will consume and destroy the value.
/// </summary>
/// <remarks>
/// The purpose of this is to ensure that any labelpair arrays are allocated from a pool and reused.
/// To facilitate this, we wrap all the arrays in this class, instead of letting the caller provide their own arrays.
/// </remarks>
public struct ExemplarLabelSet
{
    // We do not return this value to the pool, it is eternal.
    internal static readonly ExemplarLabelSet Empty = new ExemplarLabelSet(Array.Empty<Exemplar.LabelPair>(), 0);

    internal ExemplarLabelSet(Exemplar.LabelPair[] buffer, int length)
    {
        Buffer = buffer;
        Length = length;
    }

    /// <summary>
    /// The buffer containing the label pairs. Might not be fully filled!
    /// </summary>
    internal Exemplar.LabelPair[] Buffer { get; private set; }

    /// <summary>
    /// Number of label pairs from the buffer to use.
    /// </summary>
    internal int Length { get; private set; }

    internal static ExemplarLabelSet AllocateFromPool(int length)
    {
        if (length < 1)
            throw new ArgumentOutOfRangeException(nameof(length), $"{nameof(ExemplarLabelSet)} data length must be at least 1.");

        var buffer = ArrayPool<Exemplar.LabelPair>.Shared.Rent(length);

        return new ExemplarLabelSet(buffer, length);
    }

    internal void ReturnToPoolIfNotEmpty()
    {
        if (Length == 0)
            return;

        ArrayPool<Exemplar.LabelPair>.Shared.Return(Buffer);

        // Just for safety, in case it gets accidentally reused.
        Buffer = Array.Empty<Exemplar.LabelPair>();
        Length = 0;
    }
}
