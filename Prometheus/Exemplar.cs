#if NET6_0_OR_GREATER
using System.Diagnostics;
#endif
using System.Text;

namespace Prometheus;

public static class Exemplar
{
    public static readonly LabelPair[] None = Array.Empty<LabelPair>();

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
    /// </summary>
    public static LabelKey Key(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("empty key");

        Collector.ValidateLabelName(key);

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

    // Based on https://opentelemetry.io/docs/reference/specification/compatibility/prometheus_and_openmetrics/
    private static readonly LabelKey DefaultTraceIdKey = Key("trace_id");
    private static readonly LabelKey DefaultSpanIdKey = Key("span_id");

    public static LabelPair[] FromTraceContext() => FromTraceContext(DefaultTraceIdKey, DefaultSpanIdKey);

    public static LabelPair[] FromTraceContext(LabelKey traceIdKey, LabelKey spanIdKey)
    {
#if NET6_0_OR_GREATER
        var activity = Activity.Current;
        if (activity != null)
        {
            var traceIdLabel = traceIdKey.WithValue(activity.TraceId.ToString());
            var spanIdLabel = spanIdKey.WithValue(activity.SpanId.ToString());

            return new[] { traceIdLabel, spanIdLabel };
        }
#endif

        // Trace context based exemplars are only supported in .NET Core, not .NET Framework.
        return None;
    }
}