namespace Prometheus;

/// <summary>
/// A sequence of metric label-name pairs.
/// </summary>
internal readonly struct LabelSequence : IEquatable<LabelSequence>
{
    public static readonly LabelSequence Empty = new();

    public readonly StringSequence Names;
    public readonly StringSequence Values;

    public int Length => Names.Length;

    private LabelSequence(StringSequence names, StringSequence values)
    {
        if (names.Length != values.Length)
            throw new ArgumentException("The list of label values must have the same number of elements as the list of label names.");

        Names = names;
        Values = values;

        _hashCode = CalculateHashCode();
    }

    public static LabelSequence From(StringSequence names, StringSequence values)
    {
        return new LabelSequence(names, values);
    }

    public static LabelSequence From(IDictionary<string, string> dictionary)
    {
        var names = new string[dictionary.Count];
        var values = new string[dictionary.Count];

        var index = 0;

        foreach (var pair in dictionary)
        {
            names[index] = pair.Key;
            values[index] = pair.Value;
            index++;
        }

        return new LabelSequence(StringSequence.From(names), StringSequence.From(values));
    }

    /// <summary>
    /// Creates a new label sequence with some additional labels concatenated to the current sequence.
    /// </summary>
    public LabelSequence Concat(LabelSequence labels)
    {
        return new LabelSequence(Names.Concat(labels.Names), Values.Concat(labels.Values));
    }

    public bool TryGetLabelValue(string labelName, out string labelValue)
    {
        var nameEnumerator = Names.GetEnumerator();
        var valueEnumerator = Values.GetEnumerator();

        for (var i = 0; i < Names.Length; i++)
        {
            if (!nameEnumerator.MoveNext()) throw new Exception("API contract violation.");
            if (!valueEnumerator.MoveNext()) throw new Exception("API contract violation.");

            if (nameEnumerator.Current.Equals(labelName, StringComparison.Ordinal))
            {
                labelValue = valueEnumerator.Current;
                return true;
            }
        }

        labelValue = string.Empty;
        return false;
    }

    private static string EscapeLabelValue(string value)
    {
        return value
                .Replace("\\", @"\\")
                .Replace("\n", @"\n")
                .Replace("\"", @"\""");
    }

    private static int GetEscapedLabelValueByteCount(string value)
    {
        var byteCount = PrometheusConstants.ExportEncoding.GetByteCount(value);

        foreach (var c in value)
        {
            if (c == '\\' || c == '\n' || c == '"')
                byteCount++;
        }

        return byteCount;
    }

    /// <summary>
    /// Serializes to the labelkey1="labelvalue1",labelkey2="labelvalue2" label string as bytes.
    /// </summary>
    public byte[] Serialize()
    {
        // Result is cached in child collector, though we still might be making many of these child collectors if they are not reused.
        // Let's try to be efficient to avoid allocations if this gets called in a hot path.

        // First pass - calculate how many bytes we need to allocate.
        var nameEnumerator = Names.GetEnumerator();
        var valueEnumerator = Values.GetEnumerator();

        var byteCount = 0;

        for (var i = 0; i < Names.Length; i++)
        {
            if (!nameEnumerator.MoveNext()) throw new Exception("API contract violation.");
            if (!valueEnumerator.MoveNext()) throw new Exception("API contract violation.");

            if (i != 0)
                byteCount += TextSerializer.Comma.Length;

            byteCount += PrometheusConstants.ExportEncoding.GetByteCount(nameEnumerator.Current);
            byteCount += TextSerializer.Equal.Length;
            byteCount += TextSerializer.Quote.Length;
            byteCount += GetEscapedLabelValueByteCount(valueEnumerator.Current);
            byteCount += TextSerializer.Quote.Length;
        }

        var bytes = new byte[byteCount];
        var index = 0;

        nameEnumerator = Names.GetEnumerator();
        valueEnumerator = Values.GetEnumerator();

        for (var i = 0; i < Names.Length; i++)
        {
            if (!nameEnumerator.MoveNext()) throw new Exception("API contract violation.");
            if (!valueEnumerator.MoveNext()) throw new Exception("API contract violation.");

#if NET
            if (i != 0)
            {
                TextSerializer.Comma.CopyTo(bytes.AsSpan(index));
                index += TextSerializer.Comma.Length;
            }

            index += PrometheusConstants.ExportEncoding.GetBytes(nameEnumerator.Current, 0, nameEnumerator.Current.Length, bytes, index);

            TextSerializer.Equal.CopyTo(bytes.AsSpan(index));
            index += TextSerializer.Equal.Length;

            TextSerializer.Quote.CopyTo(bytes.AsSpan(index));
            index += TextSerializer.Quote.Length;

            var escapedLabelValue = EscapeLabelValue(valueEnumerator.Current);
            index += PrometheusConstants.ExportEncoding.GetBytes(escapedLabelValue, 0, escapedLabelValue.Length, bytes, index);

            TextSerializer.Quote.CopyTo(bytes.AsSpan(index));
            index += TextSerializer.Quote.Length;
#else
            if (i != 0)
            {
                Array.Copy(TextSerializer.Comma, 0, bytes, index, TextSerializer.Comma.Length);
                index += TextSerializer.Comma.Length;
            }

            index += PrometheusConstants.ExportEncoding.GetBytes(nameEnumerator.Current, 0, nameEnumerator.Current.Length, bytes, index);

            Array.Copy(TextSerializer.Equal, 0, bytes, index, TextSerializer.Equal.Length);
            index += TextSerializer.Equal.Length;

            Array.Copy(TextSerializer.Quote, 0, bytes, index, TextSerializer.Quote.Length);
            index += TextSerializer.Quote.Length;

            var escapedLabelValue = EscapeLabelValue(valueEnumerator.Current);
            index += PrometheusConstants.ExportEncoding.GetBytes(escapedLabelValue, 0, escapedLabelValue.Length, bytes, index);

            Array.Copy(TextSerializer.Quote, 0, bytes, index, TextSerializer.Quote.Length);
            index += TextSerializer.Quote.Length;
#endif
        }

        if (index != byteCount) throw new Exception("API contract violation - we counted the same bytes twice but got different numbers.");

        return bytes;
    }

    public bool Equals(LabelSequence other)
    {
        if (_hashCode != other._hashCode) return false;
        if (Length != other.Length) return false;

        return Names.Equals(other.Names) && Values.Equals(other.Values);
    }

    public override bool Equals(object? obj)
    {
        if (obj is LabelSequence ls)
            return Equals(ls);

        return false;
    }

    public override int GetHashCode() => _hashCode;

    private readonly int _hashCode;

    private int CalculateHashCode()
    {
        int hashCode = 0;

        unchecked
        {
            hashCode ^= (Names.GetHashCode() * 397);
            hashCode ^= (Values.GetHashCode() * 397);
        }

        return hashCode;
    }

    /// <summary>
    /// Converts the label sequence to a dictionary.
    /// </summary>
    public IDictionary<string, string> ToDictionary()
    {
        var result = new Dictionary<string, string>();

        var nameEnumerator = Names.GetEnumerator();
        var valueEnumerator = Values.GetEnumerator();

        for (var i = 0; i < Names.Length; i++)
        {
            if (!nameEnumerator.MoveNext()) throw new Exception("API contract violation.");
            if (!valueEnumerator.MoveNext()) throw new Exception("API contract violation.");

            result.Add(nameEnumerator.Current, valueEnumerator.Current);
        }

        return result;
    }

    public override string ToString()
    {
        // Just for debugging.
        return $"({Length})" + string.Join("; ", ToDictionary().Select(pair => $"{pair.Key} = {pair.Value}"));
    }
}
