using System.Text;

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
        ///
        /// The string is expected to only contain runes in the ASCII range, runes outside the ASCII range will get replaced
        /// with placeholders. This constraint may be relaxed with future versions.
        /// </summary>
        public LabelPair WithValue(string value)
        {
            var asciiBytes = Encoding.ASCII.GetBytes(value);
            return new LabelPair(Bytes, asciiBytes, RuneCount + asciiBytes.Length);
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
    ///
    /// The string is expected to only contain runes in the ASCII range, runes outside the ASCII range will get replaced
    /// with placeholders. This constraint may be relaxed with future versions.
    /// </summary>
    public static LabelKey Key(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("empty key");
        var asciiBytes = Encoding.ASCII.GetBytes(key);
        return new LabelKey(asciiBytes, asciiBytes.Length);
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