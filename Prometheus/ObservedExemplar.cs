using Microsoft.Extensions.ObjectPool;
using System.Diagnostics;

namespace Prometheus;

/// <summary>
/// Internal representation of an Exemplar ready to be serialized.
/// </summary>
internal class ObservedExemplar
{
    /// <summary>
    /// OpenMetrics places a length limit of 128 runes on the exemplar (sum of all key value pairs).
    /// </summary>
    private const int MaxRunes = 128;

    /// <summary>
    /// We have a pool of unused instances that we can reuse, to avoid constantly allocating memory. Once the set of metrics stabilizes,
    /// all allocations should generally be coming from the pool. We expect the default pool configuratiopn to be suitable for this.
    /// </summary>
    private static readonly ObjectPool<ObservedExemplar> Pool = ObjectPool.Create<ObservedExemplar>();

    public static readonly ObservedExemplar Empty = new();

    internal static INowProvider NowProvider = new RealNowProvider();

    public Exemplar.LabelPair[]? Labels { get; private set; }
    public double Value { get; private set; }
    public double Timestamp { get; private set; }

    public ObservedExemplar()
    {
        Labels = null;
        Value = 0;
        Timestamp = 0;
    }

    internal interface INowProvider
    {
        double Now();
    }

    private sealed class RealNowProvider : INowProvider
    {
        public double Now()
        {
            return DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1e3;
        }
    }

    public bool IsValid => Labels != null;

    private void Update(Exemplar.LabelPair[] labels, double value)
    {
        Debug.Assert(this != Empty, "Do not mutate the sentinel");

        var totalRuneCount = 0;
        for (var i = 0; i < labels.Length; i++)
        {
            totalRuneCount += labels[i].RuneCount;
            for (var j = 0; j < labels.Length; j++)
            {
                if (i == j) continue;
                if (Equal(labels[i].KeyBytes, labels[j].KeyBytes))
                    throw new ArgumentException("Exemplar contains duplicate keys.");
            }
        }

        if (totalRuneCount > MaxRunes)
            throw new ArgumentException($"Exemplar consists of {totalRuneCount} runes, exceeding the OpenMetrics limit of {MaxRunes}.");

        Labels = labels;
        Value = value;
        Timestamp = NowProvider.Now();
    }

    private static bool Equal(byte[] a, byte[] b)
    {
        // see https://www.syncfusion.com/succinctly-free-ebooks/application-security-in-net-succinctly/comparing-byte-arrays
        var x = a.Length ^ b.Length;
        for (var i = 0; i < a.Length && i < b.Length; ++i) x |= a[i] ^ b[i];
        return x == 0;
    }

    public static ObservedExemplar CreatePooled(Exemplar.LabelPair[] labelPairs, double value)
    {
        var instance = Pool.Get();
        instance.Update(labelPairs, value);
        return instance;
    }

    public static void ReturnPooledIfNotEmpty(ObservedExemplar instance)
    {
        if (object.ReferenceEquals(instance, Empty))
            return; // We never put the "Empty" instance into the pool. Do the check here to avoid repeating it any time we return instances to the pool.

        Pool.Return(instance);
    }
}