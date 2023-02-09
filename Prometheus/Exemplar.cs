#if NET6_0_OR_GREATER
using System.Buffers;
using System.Diagnostics;
#endif
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
/// </summary>
public sealed class Exemplar
{
    /// <summary>
    /// Indicates that no exemplar is to be recorded for a given observation.
    /// </summary>
    public static readonly Exemplar None = new Exemplar(Array.Empty<LabelPair>(), 0);

    /// <summary>
    /// An exemplar label key. For optimal performance, create it once and reuse it forever.
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
            var valueBytes = Encoding.ASCII.GetBytes(value);
            return new LabelPair(Bytes, valueBytes, RuneCount + valueBytes.Length);
        }
    }

    /// <summary>
    /// A single exemplar label pair in a form suitable for efficient serialization.
    /// If you wish to reuse the same key-value pair, you should reuse this object as much as possible.
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
    /// Reuse this for optimal performance.
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
    /// from these. You may (should) reuse a LabelPair for recording multiple observations that use the same exemplar.
    /// </summary>
    public static LabelPair Pair(string key, string value)
    {
        return Key(key).WithValue(value);
    }

    public static Exemplar From(in LabelPair labelPair1, in LabelPair labelPair2, in LabelPair labelPair3, in LabelPair labelPair4, in LabelPair labelPair5, in LabelPair labelPair6)
    {
        var exemplar = Exemplar.AllocateFromPool(length: 6);
        exemplar.Buffer[0] = labelPair1;
        exemplar.Buffer[1] = labelPair2;
        exemplar.Buffer[2] = labelPair3;
        exemplar.Buffer[3] = labelPair4;
        exemplar.Buffer[4] = labelPair5;
        exemplar.Buffer[5] = labelPair6;

        return exemplar;
    }

    public static Exemplar From(in LabelPair labelPair1, in LabelPair labelPair2, in LabelPair labelPair3, in LabelPair labelPair4, in LabelPair labelPair5)
    {
        var exemplar = Exemplar.AllocateFromPool(length: 5);
        exemplar.Buffer[0] = labelPair1;
        exemplar.Buffer[1] = labelPair2;
        exemplar.Buffer[2] = labelPair3;
        exemplar.Buffer[3] = labelPair4;
        exemplar.Buffer[4] = labelPair5;

        return exemplar;
    }
    
    public static Exemplar From(in LabelPair labelPair1, in LabelPair labelPair2, in LabelPair labelPair3, in LabelPair labelPair4)
    {
        var exemplar = Exemplar.AllocateFromPool(length: 4);
        exemplar.Buffer[0] = labelPair1;
        exemplar.Buffer[1] = labelPair2;
        exemplar.Buffer[2] = labelPair3;
        exemplar.Buffer[3] = labelPair4;

        return exemplar;
    }

    public static Exemplar From(in LabelPair labelPair1, in LabelPair labelPair2, in LabelPair labelPair3)
    {
        var exemplar = Exemplar.AllocateFromPool(length: 3);
        exemplar.Buffer[0] = labelPair1;
        exemplar.Buffer[1] = labelPair2;
        exemplar.Buffer[2] = labelPair3;

        return exemplar;
    }

    public static Exemplar From(in LabelPair labelPair1, in LabelPair labelPair2)
    {
        var exemplar = Exemplar.AllocateFromPool(length: 2);
        exemplar.Buffer[0] = labelPair1;
        exemplar.Buffer[1] = labelPair2;

        return exemplar;
    }

    public static Exemplar From(in LabelPair labelPair1)
    {
        var exemplar = Exemplar.AllocateFromPool(length: 1);
        exemplar.Buffer[0] = labelPair1;

        return exemplar;
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
        Buffer = Array.Empty<LabelPair>();
    }

    private Exemplar(LabelPair[] buffer, int length)
    {
        Buffer = buffer;
        Length = length;
    }

    internal void Update(LabelPair[] buffer, int length)
    {
        Buffer = buffer;
        Length = length;
        _consumed = false;
    }

    /// <summary>
    /// The buffer containing the label pairs. Might not be fully filled!
    /// </summary>
    internal LabelPair[] Buffer { get; private set; }

    /// <summary>
    /// Number of label pairs from the buffer to use.
    /// </summary>
    internal int Length { get; private set; }

    private static readonly ObjectPool<Exemplar> ExemplarPool = ObjectPool.Create<Exemplar>();

    internal static Exemplar AllocateFromPool(int length)
    {
        if (length < 1)
            throw new ArgumentOutOfRangeException(nameof(length), $"{nameof(Exemplar)} key-value pair length must be at least 1 when constructing a pool-backed value.");

#if NET
        var buffer = ArrayPool<LabelPair>.Shared.Rent(length);
#else
        // .NET Framework does not support ArrayPool, so we just allocate explicit arrays to keep it simple. Migrate to .NET Core to get better performance.
        var buffer = new LabelPair[length];
#endif

        var instance = ExemplarPool.Get();
        instance.Update(buffer, length);
        return instance;
    }

    internal void ReturnToPoolIfNotEmpty()
    {
        if (Length == 0)
            return; // Only the None instance can have a length of 0.

#if NET
        ArrayPool<LabelPair>.Shared.Return(Buffer);
#endif

        Length = 0;
        Buffer = Array.Empty<LabelPair>();

        ExemplarPool.Return(this);
    }

    private volatile bool _consumed;

    internal void MarkAsConsumed()
    {
        if (_consumed)
            throw new InvalidOperationException($"An instance of {nameof(Exemplar)} was reused. You must obtain a new instance via Exemplar.From() for each observation.");

        _consumed = true;
    }
}