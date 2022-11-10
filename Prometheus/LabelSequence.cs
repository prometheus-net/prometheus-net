namespace Prometheus;

/// <summary>
/// A sequence of metric label-name pairs.
/// </summary>
internal struct LabelSequence
{
    public static readonly LabelSequence Empty = new();

    public readonly StringSequence Names;
    public readonly StringSequence Values;

    public int Length { get; }

    private LabelSequence(StringSequence names, StringSequence values)
    {
        if (names.Length != values.Length)
            throw new ArgumentException("The list of label values must have the same number of elements as the list of label names.");

        Names = names;
        Values = values;

        Length = names.Length;

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
}
