namespace Prometheus;

public sealed record EventCounterAdapterOptions
{
    public static EventCounterAdapterOptions Default => new();

    /// <summary>
    /// By default we subscribe to a predefined set of generally useful event counters but this allows you to specify a custom filter by event source name.
    /// </summary>
    public Func<string, bool> EventSourceFilterPredicate { get; set; } = EventCounterAdapter.DefaultEventSourceFilterPredicate;

    /// <summary>
    /// By default, we subscribe to event counters at Informational level from every event source.
    /// You can customize these settings via this callback (with the event source name as the string given as input).
    /// </summary>
    public Func<string, EventCounterAdapterEventSourceSettings> EventSourceSettingsProvider { get; set; } = _ => new();

    /// <summary>
    /// How often we update event counter data.
    /// </summary>
    /// <remarks>
    /// Event counters are quite noisy in terms of generating a lot of temporary objects in memory, so we keep the default moderate.
    /// All this memory is immediately GC-able but in a near-idle app it can make for a scary upward trend on the RAM usage graph because the GC might not immediately release the memory to the OS.
    /// </remarks>
    public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromSeconds(10);

    public CollectorRegistry Registry { get; set; } = Metrics.DefaultRegistry;

    /// <summary>
    /// If set, the value in Registry is ignored and this factory is instead used to create all the metrics.
    /// </summary>
    public IMetricFactory? MetricFactory { get; set; } = Metrics.DefaultFactory;
}
