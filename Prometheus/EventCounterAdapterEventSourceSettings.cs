using System.Diagnostics.Tracing;

namespace Prometheus;

/// <summary>
/// Defines how the EventCounterAdapter will subscribe to an event source.
/// </summary>
public sealed class EventCounterAdapterEventSourceSettings
{
    /// <summary>
    /// Minimum level of events to receive.
    /// </summary>
    public EventLevel MinimumLevel { get; set; } = EventLevel.Informational;

    /// <summary>
    /// Event keywords, of which at least one must match for an event to be received.
    /// </summary>
    public EventKeywords MatchKeywords { get; set; } = EventKeywords.None;
}
