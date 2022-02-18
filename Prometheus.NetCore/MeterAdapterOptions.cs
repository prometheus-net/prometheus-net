#if NET6_0_OR_GREATER

using System;
using System.Diagnostics.Metrics;

namespace Prometheus;

public sealed class MeterAdapterOptions
{
    public static readonly MeterAdapterOptions Default = new();

    /// <summary>
    /// By default we subscribe to all meters but this allows you to filter by instrument.
    /// </summary>
    public Func<Instrument, bool> InstrumentFilterPredicate { get; set; } = _ => true;

    public CollectorRegistry Registry { get; set; } = Metrics.DefaultRegistry;
}

#endif