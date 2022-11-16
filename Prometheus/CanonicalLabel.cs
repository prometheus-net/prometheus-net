namespace Prometheus;

internal readonly struct CanonicalLabel
{
    public static readonly CanonicalLabel Empty = new(
        Array.Empty<byte>(), Array.Empty<byte>(), Array.Empty<byte>());

    public CanonicalLabel(byte[] name, byte[] prometheus, byte[] openMetrics)
    {
        Prometheus = prometheus;
        OpenMetrics = openMetrics;
        Name = name;
    }

    public byte[] Name { get;  }
    
    public byte[] Prometheus { get;  }
    public byte[] OpenMetrics { get;  }

    public bool IsNotEmpty => Name.Length > 0;
}