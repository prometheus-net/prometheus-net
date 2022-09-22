using System.Diagnostics.Metrics;

namespace Prometheus;

public sealed class MeterAdapterOptions
{
    public static readonly MeterAdapterOptions Default = new();

    // This is unlikely to be suitable for all cases, so you will want to customize it per-instrument.
    public static readonly double[] DefaultHistogramBuckets = Histogram.ExponentialBuckets(0.01, 2, 25);

    /// <summary>
    /// By default we publish all instruments from all meters but this allows you to filter by instrument.
    /// </summary>
    public Func<Instrument, bool> InstrumentFilterPredicate { get; set; } = _ => true;

    /// <summary>
    /// The .NET Meters API does not tell us (or even know itself) when a metric with a certain label combination is no longer going to receive new data.
    /// To avoid building an ever-increasing store of in-memory metrics states, we unpublish metrics once they have not been updated in a while.
    /// The idea being that metrics are useful when they are changing regularly - if a value stays the same for N minutes, it probably is not a valuable data point anymore.
    /// </summary>
    public TimeSpan MetricsExpireAfter { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Metrics will be published in this registry.
    /// </summary>
    public CollectorRegistry Registry { get; set; } = Metrics.DefaultRegistry;

    /// <summary>
    /// Enables you to define custom buckets for histogram-typed metrics.
    /// </summary>
    public Func<Instrument, double[]> ResolveHistogramBuckets { get; set; } = _ => DefaultHistogramBuckets;
}
