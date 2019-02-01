using System;
using System.Linq;

namespace Prometheus
{
    /// <summary>
    /// The set of labels and label values associated with a metric. Used both for export and as keys.
    /// </summary>
    /// <remarks>
    /// Only the values are considered for equality purposes - the caller must ensure that
    /// LabelValues objects with different sets of names are never compared to each other.
    /// </remarks>
    internal sealed class Labels : IEquatable<Labels>
    {
        public static readonly Labels Empty = new Labels(new string[0], new string[0]);

        public int Count => _names.Length;

        private readonly string[] _values;
        private readonly string[] _names;

        private readonly int _hashCode;

        public Labels(string[] names, string[] values)
        {
            if (names == null)
                throw new ArgumentNullException(nameof(names));

            if (values == null)
                throw new ArgumentNullException(nameof(values));

            if (names.Length != values.Length)
                throw new ArgumentException("Label values must be of same length as label names");

            if (values.Any(lv => lv == null))
                throw new ArgumentNullException("A label value cannot be null.");

            _values = values;
            _names = names;

            // Calculating the hash code is fast but we don't need to re-calculate it for each comparison this object is involved in.
            // Label values are fixed- caluclate it once up-front and remember the value.
            _hashCode = CalculateHashCode(_values);
        }

        public Labels Concat(params (string, string)[] more)
        {
            var allNames = _names.Concat(more.Select(m => m.Item1)).ToArray();
            var allValues = _values.Concat(more.Select(m => m.Item2)).ToArray();

            return new Labels(allNames, allValues);
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
            var labels = _names
                .Zip(_values, (name, value) => $"{name}=\"{EscapeLabelValue(value)}\"");

            return string.Join(",", labels);
        }

        public bool Equals(Labels other)
        {
            if (_hashCode != other._hashCode) return false;
            if (other._values.Length != _values.Length) return false;
            for (int i = 0; i < _values.Length; i++)
            {
                if (!string.Equals(_values[i], other._values[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Labels))
            {
                return false;
            }

            var other = (Labels)obj;
            return Equals(other);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        private static int CalculateHashCode(string[] values)
        {
            unchecked
            {
                int hashCode = 0;
                for (int i = 0; i < values.Length; i++)
                {
                    hashCode ^= (values[i].GetHashCode() * 397);
                }

                return hashCode;
            }
        }
    }
}