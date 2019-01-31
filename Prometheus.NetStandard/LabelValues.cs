using Prometheus.DataContracts;
using System;
using System.Collections.Generic;
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
    internal struct LabelValues : IEquatable<LabelValues>
    {
        public static readonly LabelValues Empty = new LabelValues(new string[0], new string[0]);

        /// <summary>
        /// These are exported with metrics. Lazy-initialized in order to save allocations when using LabelValues as keys.
        /// </summary>
        public List<LabelPair> WireLabels
        {
            get
            {
                if (_wireLabels == null)
                    _wireLabels = InitWireLabels();

                return _wireLabels;
            }
        }

        private readonly string[] _values;
        private readonly string[] _names;

        private readonly int _hashCode;

        private List<LabelPair> _wireLabels;

        public LabelValues(string[] names, string[] values)
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

            // Lazy-initialized.
            _wireLabels = null;
        }

        private List<LabelPair> InitWireLabels()
        {
            return _names
                .Zip(_values, (n, v) => new LabelPair { name = n, value = v })
                .ToList();
        }

        public bool Equals(LabelValues other)
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
            if (!(obj is LabelValues))
            {
                return false;
            }

            var other = (LabelValues)obj;
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