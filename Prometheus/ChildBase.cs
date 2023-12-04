namespace Prometheus;

/// <summary>
/// Base class for labeled instances of metrics (with all label names and label values defined).
/// </summary>
public abstract class ChildBase : ICollectorChild, IDisposable
{
    internal ChildBase(Collector parent, LabelSequence instanceLabels, LabelSequence flattenedLabels, bool publish, ExemplarBehavior exemplarBehavior)
    {
        Parent = parent;
        InstanceLabels = instanceLabels;
        FlattenedLabels = flattenedLabels;
        _publish = publish;
        _exemplarBehavior = exemplarBehavior;
    }

    private readonly ExemplarBehavior _exemplarBehavior;

    /// <summary>
    /// Marks the metric as one to be published, even if it might otherwise be suppressed.
    /// 
    /// This is useful for publishing zero-valued metrics once you have loaded data on startup and determined
    /// that there is no need to increment the value of the metric.
    /// </summary>
    /// <remarks>
    /// Subclasses must call this when their value is first set, to mark the metric as published.
    /// </remarks>
    public void Publish()
    {
        Volatile.Write(ref _publish, true);
    }

    /// <summary>
    /// Marks the metric as one to not be published.
    /// 
    /// The metric will be published when Publish() is called or the value is updated.
    /// </summary>
    public void Unpublish()
    {
        Volatile.Write(ref _publish, false);
    }

    /// <summary>
    /// Removes this labeled instance from metrics.
    /// It will no longer be published and any existing measurements/buckets will be discarded.
    /// </summary>
    public void Remove()
    {
        Parent.RemoveLabelled(InstanceLabels);
    }

    public void Dispose() => Remove();

    /// <summary>
    /// Labels specific to this metric instance, without any inherited static labels.
    /// Internal for testing purposes only.
    /// </summary>
    internal LabelSequence InstanceLabels { get; }

    /// <summary>
    /// All labels that materialize on this metric instance, including inherited static labels.
    /// Internal for testing purposes only.
    /// </summary>
    internal LabelSequence FlattenedLabels { get; }

    internal byte[] FlattenedLabelsBytes => NonCapturingLazyInitializer.EnsureInitialized(ref _flattenedLabelsBytes, this, _assignFlattenedLabelsBytesFunc)!;
    private byte[]? _flattenedLabelsBytes;
    private static readonly Action<ChildBase> _assignFlattenedLabelsBytesFunc = AssignFlattenedLabelsBytes;
    private static void AssignFlattenedLabelsBytes(ChildBase instance) => instance._flattenedLabelsBytes = instance.FlattenedLabels.Serialize();

    internal readonly Collector Parent;

    private bool _publish;

    /// <summary>
    /// Collects all the metric data rows from this collector and serializes it using the given serializer.
    /// </summary>
    /// <remarks>
    /// Subclass must check _publish and suppress output if it is false.
    /// </remarks>
    internal ValueTask CollectAndSerializeAsync(IMetricsSerializer serializer, CancellationToken cancel)
    {
        if (!Volatile.Read(ref _publish))
            return default;

        return CollectAndSerializeImplAsync(serializer, cancel);
    }

    // Same as above, just only called if we really need to serialize this metric (if publish is true).
    private protected abstract ValueTask CollectAndSerializeImplAsync(IMetricsSerializer serializer, CancellationToken cancel);

    /// <summary>
    /// Borrows an exemplar temporarily, to be later returned via ReturnBorrowedExemplar.
    /// Borrowing ensures that no other thread is modifying it (as exemplars are not thread-safe).
    /// You would typically want to do this while serializing the exemplar.
    /// </summary>
    internal static ObservedExemplar BorrowExemplar(ref ObservedExemplar storage)
    {
        return Interlocked.Exchange(ref storage, ObservedExemplar.Empty);
    }

    /// <summary>
    /// Returns a borrowed exemplar to storage or the object pool, with correct handling for cases where it is Empty.
    /// </summary>
    internal static void ReturnBorrowedExemplar(ref ObservedExemplar storage, ObservedExemplar borrowed)
    {
        if (borrowed == ObservedExemplar.Empty)
            return;

        // Return the exemplar unless a new one has arrived, in which case we discard the old one we were holding.
        var foundExemplar = Interlocked.CompareExchange(ref storage, borrowed, ObservedExemplar.Empty);

        if (foundExemplar != ObservedExemplar.Empty)
        {
            // A new exemplar had already been written, so we could not return the borrowed one. That's perfectly fine - discard it.
            ObservedExemplar.ReturnPooledIfNotEmpty(borrowed);
        }
    }

    internal void RecordExemplar(Exemplar exemplar, ref ObservedExemplar storage, double observedValue)
    {
        exemplar.MarkAsConsumed();

        // We do the "is allowed" check only if we really have an exemplar to record, to minimize the performance impact on users who do not use exemplars.
        // If you are using exemplars, you are already paying for a lot of value serialization overhead, so this is insignificant.
        // Whereas if you are not using exemplars, the difference from this simple check can be substantial.
        if (!IsRecordingNewExemplarAllowed())
        {
            // We will not record the exemplar but must still release the resources to the pool.
            exemplar.ReturnToPoolIfNotEmpty();
            return;
        }

        // ObservedExemplar takes ownership of the Exemplar and will return its resources to the pool when the time is right.
        var observedExemplar = ObservedExemplar.CreatePooled(exemplar, observedValue);
        ObservedExemplar.ReturnPooledIfNotEmpty(Interlocked.Exchange(ref storage, observedExemplar));
        MarkNewExemplarHasBeenRecorded();

        // We cannot record an exemplar every time we record an exemplar!
        Volatile.Read(ref ExemplarsRecorded)?.Inc(Exemplar.None);
    }

    protected Exemplar GetDefaultExemplar(double value)
    {
        if (_exemplarBehavior.DefaultExemplarProvider == null)
            return Exemplar.None;

        return _exemplarBehavior.DefaultExemplarProvider(Parent, value);
    }

    // May be replaced in test code.
    internal static Func<double> ExemplarRecordingTimestampProvider = DefaultExemplarRecordingTimestampProvider;
    internal static double DefaultExemplarRecordingTimestampProvider() => LowGranularityTimeSource.GetSecondsFromUnixEpoch();

    // Timetamp of when we last recorded an exemplar. We do not use ObservedExemplar.Timestamp because we do not want to
    // read from an existing ObservedExemplar when we are writing to our metrics (to avoid the synchronization overhead).
    // We start at a deep enough negative value to not cause funny behavior near zero point (only likely in tests, really).
    private ThreadSafeDouble _exemplarLastRecordedTimestamp = new(-100_000_000);

    protected bool IsRecordingNewExemplarAllowed()
    {
        if (_exemplarBehavior.NewExemplarMinInterval <= TimeSpan.Zero)
            return true;

        var elapsedSeconds = ExemplarRecordingTimestampProvider() - _exemplarLastRecordedTimestamp.Value;

        return elapsedSeconds >= _exemplarBehavior.NewExemplarMinInterval.TotalSeconds;
    }

    protected void MarkNewExemplarHasBeenRecorded()
    {
        if (_exemplarBehavior.NewExemplarMinInterval <= TimeSpan.Zero)
            return; // No need to record the timestamp if we are not enforcing a minimum interval.

        _exemplarLastRecordedTimestamp.Value = ExemplarRecordingTimestampProvider();
    }


    // This is only set if and when debug metrics are enabled in the default registry.
    private static Counter? ExemplarsRecorded;

    static ChildBase()
    {
        Metrics.DefaultRegistry.OnStartCollectingRegistryMetrics(delegate
        {
            Volatile.Write(ref ExemplarsRecorded, Metrics.CreateCounter("prometheus_net_exemplars_recorded_total", "Number of exemplars that were accepted into in-memory storage in the prometheus-net SDK."));
        });
    }

    public override string ToString()
    {
        // Just for debugging.
        return $"{Parent.Name}{{{FlattenedLabels}}}";
    }
}