using System.Globalization;

namespace Prometheus;

public static class Exemplar
{
    /// <summary>
    /// An exemplar label key.
    /// </summary>
    public readonly struct LabelKey
    {
        internal LabelKey(byte[] key, int runeCount)
        {
            Bytes = key;
            RuneCount = runeCount;
        }

        internal int RuneCount { get; }

        internal byte[] Bytes { get; }

        /// <summary>
        /// Create a LabelPair once a value is available
        /// </summary>
        public LabelPair WithValue(string value)
        {
            // TODO find a way of counting runes without using the StringInfo. Or find a way of pooling these :cry:
            // It is used in K as well.
            var si = new StringInfo(value);
            return new LabelPair(
                Bytes,
                PrometheusConstants.ExportEncoding.GetBytes(value),
                RuneCount + si.LengthInTextElements);
        }
    }

    /// <summary>
    /// A single exemplar label pair in a form suitable for efficient serialization.
    /// </summary>
    public readonly struct LabelPair
    {
        internal LabelPair(byte[] keyBytes, byte[] valueBytes, int runeCount)
        {
            KeyBytes = keyBytes;
            ValueBytes = valueBytes;
            RuneCount = runeCount;
        }

        internal int RuneCount { get; }
        internal byte[] KeyBytes { get; }
        internal byte[] ValueBytes { get; }
    }
    
    /// <summary>
    /// Return an exemplar label key, this may be curried with a value to produce a LabelPair.
    /// </summary>
    public static LabelKey Key(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("empty key");
        
        var si = new StringInfo(key);
        return new LabelKey(PrometheusConstants.ExportEncoding.GetBytes(key), si.LengthInTextElements);
    }
    
    /// <summary>
    /// Pair constructs a LabelPair, it is advisable to memoize a "Key" (eg: "traceID") and then to derive "LabelPair"s
    /// from these.
    /// </summary>
    public static LabelPair Pair(string key, string value)
    {
        return Key(key).WithValue(value);
    }
}