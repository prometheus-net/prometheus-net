using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace Prometheus;

/// <summary>
/// A fully-formed exemplar, describing a set of label name-value pairs.
/// 
/// One-time use only - when you pass an instance to a prometheus-net method, it will take ownership of it.
/// 
/// You should preallocate and cache:
/// 1. The exemplar keys created via Exemplar.Key().
/// 2. Exemplar key-value pairs created vvia key.WithValue() or Exemplar.Pair().
/// 
/// From the key-value pairs you can create one-use Exemplar values using Exemplar.From().
/// You can clone Exemplar instances using Exemplar.Clone() - each clone can only be used once!
/// </summary>
public sealed class Exemplar
{
    /// <summary>
    /// Indicates that no exemplar is to be recorded for a given observation.
    /// </summary>
    public static readonly Exemplar None = new(0);

    /// <summary>
    /// An exemplar label key. For optimal performance, create it once and reuse it forever.
    /// </summary>
    public readonly struct LabelKey
    {
        internal LabelKey(byte[] key)
        {
            Bytes = key;
        }

        // We only support ASCII here, so rune count always matches byte count.
        internal int RuneCount => Bytes.Length;

        internal byte[] Bytes { get; }

        /// <summary>
        /// Create a LabelPair once a value is available
        ///
        /// The string is expected to only contain runes in the ASCII range, runes outside the ASCII range will get replaced
        /// with placeholders. This constraint may be relaxed with future versions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LabelPair WithValue(string value)
        {
            static bool IsAscii(ReadOnlySpan<char> chars)
            {
                for (var i = 0; i < chars.Length; i++)
                    if (chars[i] > 127)
                        return false;

                return true;
            }

            if (!IsAscii(value.AsSpan()))
            {
                // We believe that approximately 100% of use cases only consist of ASCII characters.
                // That being said, we do not want to throw an exception here as the value may be coming from external sources
                // that calling code has little control over. Therefore, we just replace such characters with placeholders.
                // This matches the default behavior of Encoding.ASCII.GetBytes() - it replaces non-ASCII characters with '?'.
                // As this is a highly theoretical case, we do an inefficient conversion here using the built-in encoder.
                value = Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(value));
            }

            return new LabelPair(Bytes, value);
        }
    }

    /// <summary>
    /// A single exemplar label pair in a form suitable for efficient serialization.
    /// If you wish to reuse the same key-value pair, you should reuse this object as much as possible.
    /// </summary>
    public readonly struct LabelPair
    {
        internal LabelPair(byte[] keyBytes, string value)
        {
            KeyBytes = keyBytes;
            Value = value;
        }

        internal int RuneCount => KeyBytes.Length + Value.Length;
        internal byte[] KeyBytes { get; }

        // We keep the value as a string because it typically starts out its life as a string
        // and we want to avoid paying the cost of converting it to a byte array until we serialize it.
        // If we record many exemplars then we may, in fact, never serialize most of them because they get replaced.
        internal string Value { get; }
    }

    /// <summary>
    /// Return an exemplar label key, this may be curried with a value to produce a LabelPair.
    /// Reuse this for optimal performance.
    /// </summary>
    public static LabelKey Key(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("empty key", nameof(key));

        Collector.ValidateLabelName(key);

        var asciiBytes = Encoding.ASCII.GetBytes(key);
        return new LabelKey(asciiBytes);
    }

    /// <summary>
    /// Pair constructs a LabelPair, it is advisable to memoize a "Key" (eg: "traceID") and then to derive "LabelPair"s
    /// from these. You may (should) reuse a LabelPair for recording multiple observations that use the same exemplar.
    /// </summary>
    public static LabelPair Pair(string key, string value)
    {
        return Key(key).WithValue(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Exemplar From(in LabelPair labelPair1, in LabelPair labelPair2, in LabelPair labelPair3, in LabelPair labelPair4, in LabelPair labelPair5, in LabelPair labelPair6)
    {
        var exemplar = Exemplar.AllocateFromPool(length: 6);
        exemplar.LabelPair1 = labelPair1;
        exemplar.LabelPair2 = labelPair2;
        exemplar.LabelPair3 = labelPair3;
        exemplar.LabelPair4 = labelPair4;
        exemplar.LabelPair5 = labelPair5;
        exemplar.LabelPair6 = labelPair6;

        return exemplar;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Exemplar From(in LabelPair labelPair1, in LabelPair labelPair2, in LabelPair labelPair3, in LabelPair labelPair4, in LabelPair labelPair5)
    {
        var exemplar = Exemplar.AllocateFromPool(length: 5);
        exemplar.LabelPair1 = labelPair1;
        exemplar.LabelPair2 = labelPair2;
        exemplar.LabelPair3 = labelPair3;
        exemplar.LabelPair4 = labelPair4;
        exemplar.LabelPair5 = labelPair5;

        return exemplar;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Exemplar From(in LabelPair labelPair1, in LabelPair labelPair2, in LabelPair labelPair3, in LabelPair labelPair4)
    {
        var exemplar = Exemplar.AllocateFromPool(length: 4);
        exemplar.LabelPair1 = labelPair1;
        exemplar.LabelPair2 = labelPair2;
        exemplar.LabelPair3 = labelPair3;
        exemplar.LabelPair4 = labelPair4;

        return exemplar;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Exemplar From(in LabelPair labelPair1, in LabelPair labelPair2, in LabelPair labelPair3)
    {
        var exemplar = Exemplar.AllocateFromPool(length: 3);
        exemplar.LabelPair1 = labelPair1;
        exemplar.LabelPair2 = labelPair2;
        exemplar.LabelPair3 = labelPair3;

        return exemplar;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Exemplar From(in LabelPair labelPair1, in LabelPair labelPair2)
    {
        var exemplar = Exemplar.AllocateFromPool(length: 2);
        exemplar.LabelPair1 = labelPair1;
        exemplar.LabelPair2 = labelPair2;

        return exemplar;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Exemplar From(in LabelPair labelPair1)
    {
        var exemplar = Exemplar.AllocateFromPool(length: 1);
        exemplar.LabelPair1 = labelPair1;

        return exemplar;
    }

    internal ref LabelPair this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (index == 0) return ref LabelPair1;
            if (index == 1) return ref LabelPair2;
            if (index == 2) return ref LabelPair3;
            if (index == 3) return ref LabelPair4;
            if (index == 4) return ref LabelPair5;
            if (index == 5) return ref LabelPair6;
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    // Based on https://opentelemetry.io/docs/reference/specification/compatibility/prometheus_and_openmetrics/
    private static readonly LabelKey DefaultTraceIdKey = Key("trace_id");
    private static readonly LabelKey DefaultSpanIdKey = Key("span_id");

    public static Exemplar FromTraceContext() => FromTraceContext(DefaultTraceIdKey, DefaultSpanIdKey);

    public static Exemplar FromTraceContext(in LabelKey traceIdKey, in LabelKey spanIdKey)
    {
#if NET6_0_OR_GREATER
        var activity = Activity.Current;
        if (activity != null)
        {
            // These values already exist as strings inside the Activity logic, so there is no string allocation happening here.
            var traceIdLabel = traceIdKey.WithValue(activity.TraceId.ToString());
            var spanIdLabel = spanIdKey.WithValue(activity.SpanId.ToString());

            return From(traceIdLabel, spanIdLabel);
        }
#endif

        // Trace context based exemplars are only supported in .NET Core, not .NET Framework.
        return None;
    }

    public Exemplar()
    {
    }

    private Exemplar(int length)
    {
        Length = length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Update(int length)
    {
        Length = length;
        Interlocked.Exchange(ref _consumed, IsNotConsumed);
    }

    /// <summary>
    /// Number of label pairs in use.
    /// </summary>
    internal int Length { get; private set; }

    internal LabelPair LabelPair1;
    internal LabelPair LabelPair2;
    internal LabelPair LabelPair3;
    internal LabelPair LabelPair4;
    internal LabelPair LabelPair5;
    internal LabelPair LabelPair6;

    private static readonly ObjectPool<Exemplar> ExemplarPool = ObjectPool.Create<Exemplar>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Exemplar AllocateFromPool(int length)
    {
        var instance = ExemplarPool.Get();
        instance.Update(length);
        return instance;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReturnToPoolIfNotEmpty()
    {
        if (Length == 0)
            return; // Only the None instance can have a length of 0.

        Length = 0;

        ExemplarPool.Return(this);
    }

    private long _consumed;

    private const long IsConsumed = 1;
    private const long IsNotConsumed = 0;

    internal void MarkAsConsumed()
    {
        if (Interlocked.Exchange(ref _consumed, IsConsumed) == IsConsumed)
            throw new InvalidOperationException($"An instance of {nameof(Exemplar)} was reused. You must obtain a new instance via Exemplar.From() or Exemplar.Clone() for each metric value observation.");
    }

    /// <summary>
    /// Clones the exemplar so it can be reused - each copy can only be used once!
    /// </summary>
    public Exemplar Clone()
    {
        if (Interlocked.Read(ref _consumed) == IsConsumed)
            throw new InvalidOperationException($"An instance of {nameof(Exemplar)} cannot be cloned after it has already been used.");

        var clone = AllocateFromPool(Length);
        clone.LabelPair1 = LabelPair1;
        clone.LabelPair2 = LabelPair2;
        clone.LabelPair3 = LabelPair3;
        clone.LabelPair4 = LabelPair4;
        clone.LabelPair5 = LabelPair5;
        clone.LabelPair6 = LabelPair6;
        return clone;
    }
}