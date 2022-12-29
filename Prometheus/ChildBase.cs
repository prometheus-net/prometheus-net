namespace Prometheus;

/// <summary>
/// Base class for labeled instances of metrics (with all label names and label values defined).
/// </summary>
public abstract class ChildBase : ICollectorChild, IDisposable
{
    internal ChildBase(Collector parent, LabelSequence instanceLabels, LabelSequence flattenedLabels, bool publish)
    {
        Parent = parent;
        InstanceLabels = instanceLabels;
        FlattenedLabels = flattenedLabels;
        FlattenedLabelsBytes = PrometheusConstants.ExportEncoding.GetBytes(flattenedLabels.Serialize());
        _publish = publish;
    }

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

    internal byte[] FlattenedLabelsBytes { get; }

    internal readonly Collector Parent;

    private bool _publish;

    /// <summary>
    /// Collects all the metric data rows from this collector and serializes it using the given serializer.
    /// </summary>
    /// <remarks>
    /// Subclass must check _publish and suppress output if it is false.
    /// </remarks>
    internal Task CollectAndSerializeAsync(IMetricsSerializer serializer, CancellationToken cancel)
    {
        if (!Volatile.Read(ref _publish))
            return Task.CompletedTask;

        return CollectAndSerializeImplAsync(serializer, cancel);
    }

    // Same as above, just only called if we really need to serialize this metric (if publish is true).
    private protected abstract Task CollectAndSerializeImplAsync(IMetricsSerializer serializer, CancellationToken cancel);

    /// <summary>
    /// Borrows an exemplar temporarily, to be later returned via ReturnBorrowedExemplar.
    /// Borrowing ensures that no other thread is modifying it (as exemplars are not thread-safe).
    /// You would typically want to do this while serializing the exemplar.
    /// </summary>
    internal ObservedExemplar BorrowExemplar(ref ObservedExemplar storage)
    {
        return Interlocked.Exchange(ref storage, ObservedExemplar.Empty);
    }

    /// <summary>
    /// Returns a borrowed exemplar to storage or the object pool, with correct handling for cases where it is Empty.
    /// </summary>
    internal void ReturnBorrowedExemplar(ref ObservedExemplar storage, ObservedExemplar borrowed)
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
}