﻿namespace Prometheus;

/// <summary>
/// Internal representation of an Exemplar ready to be serialized.
/// </summary>
internal struct ObservedExemplar
{
    private const int MaxRunes = 128;
    public static readonly ObservedExemplar Empty = new();

    internal static INowProvder NowProvider = new RealNowProvider();

    public Exemplar.LabelPair[]? Labels { get; private set; }
    public double Val { get; private set; }
    public double Timestamp { get; private set; }

    public ObservedExemplar()
    {
        Labels = null;
        Val = 0;
        Timestamp = 0;
    }

   internal interface INowProvder
    {
        double Now();
    }

   private sealed class RealNowProvider : INowProvder
   {
       public double Now()
       { 
           return DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1e3;
       }
   }
    
    public bool IsValid => Labels != null;

    public void Update(Exemplar.LabelPair[] labels, double val)
    {
        var tally = 0;
        for (var i = 0; i < labels.Length; i++)
        {
            tally += labels[i].RuneCount;
            for (var j = 0; j < labels.Length; j++)
            {
                if (i == j) continue;
                if (Equal(labels[i].KeyBytes, labels[j].KeyBytes))
                    throw new ArgumentException("exemplar contains duplicate keys");
            }
        }

        if (tally > MaxRunes)
            throw new ArgumentException($"exemplar labels has {tally} runes, exceeding the limit of {MaxRunes}.");

        Labels = labels;
        Val = val;
        Timestamp = NowProvider.Now();
    }
    
    private static bool Equal(byte[] a, byte[] b)
    {
        var x = a.Length ^ b.Length;
        for (var i = 0; i < a.Length && i < b.Length; ++i) x |= a[i] ^ b[i];
        return x == 0;
    }
}