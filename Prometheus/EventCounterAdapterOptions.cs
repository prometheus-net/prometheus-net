namespace Prometheus
{
    public sealed class EventCounterAdapterOptions
    {
        public static readonly EventCounterAdapterOptions Default = new();

        /// <summary>
        /// By default we subscribe to event counters from all event sources but this allows you to filter by event source name.
        /// </summary>
        public Func<string, bool> EventSourceFilterPredicate { get; set; } = _ => true;

        /// <summary>
        /// By default, we subscribe to event counters at Informational level from every event source.
        /// You can customize these settings via this callback (with the event source name as the string given as input).
        /// </summary>
        public Func<string, EventCounterAdapterEventSourceSettings> EventSourceSettingsProvider { get; set; } = _ => new();

        public CollectorRegistry Registry { get; set; } = Metrics.DefaultRegistry;
    }
}
