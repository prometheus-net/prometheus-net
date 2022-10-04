using System.Text;

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

    /// <summary>
    /// Serializes to the labelkey1="labelvalue1",labelkey2="labelvalue2" label string.
    /// </summary>
    public string Serialize()
    {
        // Result is cached in child collector - no need to worry about efficiency here.

        var sb = new StringBuilder();

        var nameEnumerator = Names.GetEnumerator();
        var valueEnumerator = Values.GetEnumerator();

        for (var i = 0; i < Names.Length; i++)
        {
            if (!nameEnumerator.MoveNext()) throw new Exception("API contract violation.");
            if (!valueEnumerator.MoveNext()) throw new Exception("API contract violation.");

            if (i != 0)
                sb.Append(',');

            sb.Append(nameEnumerator.Current);
            sb.Append('=');
            sb.Append('"');
            sb.Append(EscapeLabelValue(valueEnumerator.Current));
            sb.Append('"');
        }

        return sb.ToString();
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
