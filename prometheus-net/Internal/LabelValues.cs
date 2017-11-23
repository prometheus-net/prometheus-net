using System;
using System.Collections.Generic;
using System.Linq;
using Prometheus.Advanced.DataContracts;

namespace Prometheus.Internal
{
    internal struct LabelValues : IEquatable<LabelValues>
    {
        private readonly int _hashCode;
        private readonly string[] _values;
        private readonly string[] _names;
        
        internal List<LabelPair> WireLabels; 
        internal static readonly LabelValues Empty = new LabelValues(new string[0], new string[0])
        {
            WireLabels = new List<LabelPair>()
        };

        public LabelValues(string[] names, string[] values)
        {
            if (names.Length!=values.Length)
            {
                throw new InvalidOperationException("Label values must be of same length as label names");
            }
            
            _values = values;
            _names = names;
            
            // Calculating the hash code is fast but we don't need to re-calculate it for each comparison this object is involved in.
            // Label values are fixed- caluclate it once up-front and remember the value.
            _hashCode = CalculateHashCode(_values);
            WireLabels = null;
        }

        internal void InitWireLabels()
        {
            WireLabels = new List<LabelPair>();
            WireLabels.AddRange(_names.Zip(_values, (s, s1) => new LabelPair() {name = s, value = s1}));
        }

        public bool Equals(LabelValues other)
        {
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

        public override string ToString()
        {
            throw new NotSupportedException();
            //var sb = new StringBuilder();
            //foreach (var label in _labels)
            //{
            //    sb.AppendFormat("{0}={1}, ", label.Key, label.Value);
            //}

            //return sb.ToString();
        }
    }
}