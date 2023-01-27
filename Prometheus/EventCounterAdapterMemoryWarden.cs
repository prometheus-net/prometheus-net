namespace Prometheus;

/// <summary>
/// .NET EventCounters are very noisy in terms of generating a lot of garbage. At the same time, apps in development environments typically do not get loaded much, so rarely collect garbage.
/// This can mean that as soon as you plug prometheus-net into an app, its memory usage shoots up due to gen 0 garbage piling up. It will all get collected... eventually, when the GC runs.
/// This might not happen for 12+ hours! It presents a major user perception issue, as they just see the process memory usage rise and rise and rise.
/// 
/// This class exists to prevent this problem. We simply force a gen 0 GC every N minutes if EventCounterAdapter is enabled and if no GC has occurred in the last N minutes already.
/// </summary>
internal static class EventCounterAdapterMemoryWarden
{
    private static readonly TimeSpan ForcedCollectionInterval = TimeSpan.FromMinutes(10);

    public static void EnsureStarted()
    {
        // The constructor does all the work, this is just here to signal intent.
    }

    static EventCounterAdapterMemoryWarden()
    {
        Task.Run(Execute);
    }

    private static async Task Execute()
    {
        while (true)
        {
            // Capture pre-delay state so we can check if a collection is required.
            var preDelayCollectionCount = GC.CollectionCount(0);

            await Task.Delay(ForcedCollectionInterval);

            var postDelayCollectionCount = GC.CollectionCount(0);

            if (preDelayCollectionCount != postDelayCollectionCount)
                continue; // GC already happened, go chill.

            GC.Collect(0);
        }
    }
}
