namespace Prometheus;

internal readonly struct CanonicalLabel(byte[] name, byte[] prometheus, byte[] openMetrics)
{
    public static readonly CanonicalLabel Empty = new([], [], []);

    public byte[] Name { get; } = name;

    public byte[] Prometheus { get; } = prometheus;
    public byte[] OpenMetrics { get; } = openMetrics;

    public bool IsNotEmpty => Name.Length > 0;
}