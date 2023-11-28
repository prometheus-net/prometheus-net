using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.ObjectPool;

namespace Prometheus;

/// <summary>
/// Base class for metrics, defining the basic informative API and the internal API.
/// </summary>
/// <remarks>
/// Many of the fields are lazy-initialized to ensure we only perform the memory allocation if and when we actually use them.
/// For some, it means rarely used members are never allocated at all (e.g. if you never inspect the set of label names, they are never allocated).
/// For others, it means they are allocated at first time of use (e.g. serialization-related fields are allocated when serializing the first time).
/// </remarks>
public abstract class Collector
{
    /// <summary>
    /// The metric name, e.g. http_requests_total.
    /// </summary>
    public string Name { get; }

    internal byte[] NameBytes => NonCapturingLazyInitializer.EnsureInitialized(ref _nameBytes, this, _assignNameBytesFunc)!;
    private byte[]? _nameBytes;
    private static readonly Action<Collector> _assignNameBytesFunc;
    private static void AssignNameBytes(Collector instance) => instance._nameBytes = PrometheusConstants.ExportEncoding.GetBytes(instance.Name);

    /// <summary>
    /// The help text describing the metric for a human audience.
    /// </summary>
    public string Help { get; }

    internal byte[] HelpBytes => NonCapturingLazyInitializer.EnsureInitialized(ref _helpBytes, this, _assignHelpBytesFunc)!;
    private byte[]? _helpBytes;
    private static readonly Action<Collector> _assignHelpBytesFunc;
    private static void AssignHelpBytes(Collector instance) =>
        instance._helpBytes = string.IsNullOrWhiteSpace(instance.Help) ? [] : PrometheusConstants.ExportEncoding.GetBytes(instance.Help);

    /// <summary>
    /// Names of the instance-specific labels (name-value pairs) that apply to this metric.
    /// When the values are added to the names, you get a <see cref="ChildBase"/> instance.
    /// Does not include any static label names (from metric configuration, factory or registry).
    /// </summary>
    public string[] LabelNames => NonCapturingLazyInitializer.EnsureInitialized(ref _labelNames, this, _assignLabelNamesFunc)!;
    private string[]? _labelNames;
    private static readonly Action<Collector> _assignLabelNamesFunc;
    private static void AssignLabelNames(Collector instance) => instance._labelNames = instance.InstanceLabelNames.ToArray();

    internal StringSequence InstanceLabelNames;
    internal StringSequence FlattenedLabelNames;

    /// <summary>
    /// All static labels obtained from any hierarchy level (either defined in metric configuration or in registry).
    /// These will be merged with the instance-specific labels to arrive at the final flattened label sequence for a specific child.
    /// </summary>
    internal LabelSequence StaticLabels;

    internal abstract MetricType Type { get; }

    internal byte[] TypeBytes { get; }

    internal abstract int ChildCount { get; }
    internal abstract int TimeseriesCount { get; }

    internal abstract ValueTask CollectAndSerializeAsync(IMetricsSerializer serializer, bool writeFamilyDeclaration, CancellationToken cancel);

    // Used by ChildBase.Remove()
    internal abstract void RemoveLabelled(LabelSequence instanceLabels);

    private const string ValidMetricNameExpression = "^[a-zA-Z_][a-zA-Z0-9_]*$";
    private const string ValidLabelNameExpression = "^[a-zA-Z_][a-zA-Z0-9_]*$";
    private const string ReservedLabelNameExpression = "^__.*$";

    private static readonly Regex MetricNameRegex = new Regex(ValidMetricNameExpression, RegexOptions.Compiled);
    private static readonly Regex LabelNameRegex = new Regex(ValidLabelNameExpression, RegexOptions.Compiled);
    private static readonly Regex ReservedLabelRegex = new Regex(ReservedLabelNameExpression, RegexOptions.Compiled);

    static Collector()
    {
        _assignNameBytesFunc = AssignNameBytes;
        _assignHelpBytesFunc = AssignHelpBytes;
        _assignLabelNamesFunc = AssignLabelNames;
    }

    internal Collector(string name, string help, StringSequence instanceLabelNames, LabelSequence staticLabels)
    {
        if (!MetricNameRegex.IsMatch(name))
            throw new ArgumentException($"Metric name '{name}' does not match regex '{ValidMetricNameExpression}'.");

        Name = name;
        TypeBytes = TextSerializer.MetricTypeToBytes[Type];
        Help = help;
        InstanceLabelNames = instanceLabelNames;
        StaticLabels = staticLabels;

        FlattenedLabelNames = instanceLabelNames.Concat(staticLabels.Names);

        // Used to check uniqueness of label names, to catch any label layering mistakes early.
        var uniqueLabelNames = LabelValidationHashSetPool.Get();

        try
        {
            var labelNameEnumerator = FlattenedLabelNames.GetEnumerator();
            while (labelNameEnumerator.MoveNext())
            {
                var labelName = labelNameEnumerator.Current;

                if (labelName == null)
                    throw new ArgumentNullException("Label name was null.");

                ValidateLabelName(labelName);
                uniqueLabelNames.Add(labelName);
            }

            // Here we check for label name collision, ensuring that the same label name is not defined twice on any label inheritance level.
            if (uniqueLabelNames.Count != FlattenedLabelNames.Length)
                throw new InvalidOperationException("The set of label names includes duplicates: " + string.Join(", ", FlattenedLabelNames.ToArray()));
        }
        finally
        {
            LabelValidationHashSetPool.Return(uniqueLabelNames);
        }
    }

    private static readonly ObjectPool<HashSet<string>> LabelValidationHashSetPool = ObjectPool.Create(new LabelValidationHashSetPoolPolicy());

    private sealed class LabelValidationHashSetPoolPolicy : PooledObjectPolicy<HashSet<string>>
    {
        // If something should explode the size, we do not return it to the pool.
        // This should be more than generous even for the most verbosely labeled scenarios.
        private const int PooledHashSetMaxSize = 50;

#if NET
        public override HashSet<string> Create() => new(PooledHashSetMaxSize, StringComparer.Ordinal);
#else
        public override HashSet<string> Create() => new(StringComparer.Ordinal);
#endif

        public override bool Return(HashSet<string> obj)
        {
            if (obj.Count > PooledHashSetMaxSize)
                return false;

            obj.Clear();
            return true;
        }
    }

    internal static void ValidateLabelName(string labelName)
    {
        if (!LabelNameRegex.IsMatch(labelName))
            throw new ArgumentException($"Label name '{labelName}' does not match regex '{ValidLabelNameExpression}'.");

        if (ReservedLabelRegex.IsMatch(labelName))
            throw new ArgumentException($"Label name '{labelName}' is not valid - labels starting with double underscore are reserved!");
    }

    public override string ToString()
    {
        // Just for debugging.
        return $"{Name}{{{FlattenedLabelNames}}}";
    }
}

/// <summary>
/// Base class for metrics collectors, providing common labeled child management functionality.
/// </summary>
public abstract class Collector<TChild> : Collector, ICollector<TChild>
    where TChild : ChildBase
{
    // Keyed by the instance labels (not by flattened labels!).
    private readonly Dictionary<LabelSequence, TChild> _children = new();
    private readonly ReaderWriterLockSlim _childrenLock = new();

    // Lazy-initialized since not every collector will use a child with no labels.
    // Lazy instance will be replaced if the unlabelled timeseries is removed.
    private Lazy<TChild> _unlabelledLazy;

    /// <summary>
    /// Gets the child instance that has no labels.
    /// </summary>
    protected internal TChild Unlabelled => _unlabelledLazy.Value;

    // We need it for the ICollector interface but using this is rarely relevant in client code, so keep it obscured.
    TChild ICollector<TChild>.Unlabelled => Unlabelled;

    // This servers a slightly silly but useful purpose: by default if you start typing .La... and trigger Intellisense
    // it will often for whatever reason focus on LabelNames instead of Labels, leading to tiny but persistent frustration.
    // Having WithLabels() instead eliminates the other candidate and allows for a frustration-free typing experience.
    public TChild WithLabels(params string[] labelValues) => Labels(labelValues);

    // Discourage it as it can create confusion. But it works fine, so no reason to mark it obsolete, really.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public TChild Labels(params string[] labelValues)
    {
        if (labelValues == null)
            throw new ArgumentNullException(nameof(labelValues));

        var labels = LabelSequence.From(InstanceLabelNames, StringSequence.From(labelValues));
        return GetOrAddLabelled(labels);
    }

    public void RemoveLabelled(params string[] labelValues)
    {
        if (labelValues == null)
            throw new ArgumentNullException(nameof(labelValues));

        var labels = LabelSequence.From(InstanceLabelNames, StringSequence.From(labelValues));
        RemoveLabelled(labels);
    }

    internal override void RemoveLabelled(LabelSequence labels)
    {
        _childrenLock.EnterWriteLock();

        try
        {
            _children.Remove(labels);
        }
        finally
        {
            _childrenLock.ExitWriteLock();
        }

        if (labels.Length == 0)
        {
            // If we remove the unlabeled instance (technically legitimate, if the caller really desires to do so) then
            // we need to also ensure that the special-casing used for it gets properly wired up the next time.
            _unlabelledLazy = GetUnlabelledLazyInitializer();
        }
    }

    private Lazy<TChild> GetUnlabelledLazyInitializer()
    {
        return new Lazy<TChild>(() => GetOrAddLabelled(LabelSequence.Empty));
    }

    internal override int ChildCount
    {
        get
        {
            _childrenLock.EnterReadLock();

            try
            {
                return _children.Count;
            }
            finally
            {
                _childrenLock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Gets the instance-specific label values of all labelled instances of the collector.
    /// Values of any inherited static labels are not returned in the result.
    /// 
    /// Note that during concurrent operation, the set of values returned here
    /// may diverge from the latest set of values used by the collector.
    /// </summary>
    public IEnumerable<string[]> GetAllLabelValues()
    {
        // We are yielding here so make a defensive copy so we do not hold locks for a long time.
        // We reuse this buffer, so it should be relatively harmless in the long run.
        LabelSequence[] buffer;

        _childrenLock.EnterReadLock();

        var childCount = _children.Count;
        buffer = ArrayPool<LabelSequence>.Shared.Rent(childCount);

        try
        {
            try
            {
                _children.Keys.CopyTo(buffer, 0);
            }
            finally
            {
                _childrenLock.ExitReadLock();
            }

            for (var i = 0; i < childCount; i++)
            {
                var labels = buffer[i];

                if (labels.Length == 0)
                    continue; // We do not return the "unlabelled" label set.

                // Defensive copy.
                yield return labels.Values.ToArray();
            }
        }
        finally
        {
            ArrayPool<LabelSequence>.Shared.Return(buffer);
        }
    }

    private TChild GetOrAddLabelled(LabelSequence instanceLabels)
    {
        // NOTE: We do not try to find a metric instance with the same set of label names but in a DIFFERENT order.
        // Order of labels matterns in data creation, although does not matter when the exported data set is imported later.
        // If we somehow end up registering the same metric with the same label names in different order, we will publish it twice, in two orders...
        // That is not ideal but also not that big of a deal to justify a lookup every time a metric instance is registered.

        // First try to find an existing instance. This is the fast path, if we are re-looking-up an existing one.
        _childrenLock.EnterReadLock();

        try
        {
            if (_children.TryGetValue(instanceLabels, out var existing))
                return existing;
        }
        finally
        {
            _childrenLock.ExitReadLock();
        }

        // If no existing one found, grab the write lock and create a new one if needed.
        _childrenLock.EnterWriteLock();

        try
        {
            if (_children.TryGetValue(instanceLabels, out var existing))
                return existing;

            var newChild = _createdLabelledChildFunc(instanceLabels);
            _children.Add(instanceLabels, newChild);
            return newChild;
        }
        finally
        {
            _childrenLock.ExitWriteLock();
        }
    }

    private TChild CreateLabelledChild(LabelSequence instanceLabels)
    {
        // Order of labels is 1) instance labels; 2) static labels.
        var flattenedLabels = instanceLabels.Concat(StaticLabels);

        return NewChild(instanceLabels, flattenedLabels, publish: !_suppressInitialValue, _exemplarBehavior);
    }

    // Cache the delegate to avoid allocating a new one every time in GetOrAddLabelled.
    private readonly Func<LabelSequence, TChild> _createdLabelledChildFunc;

    /// <summary>
    /// For tests that want to see what instance-level label values were used when metrics were created.
    /// This is for testing only, so does not respect locks - do not use this in concurrent context.
    /// </summary>
    internal LabelSequence[] GetAllInstanceLabelsUnsafe() => _children.Keys.ToArray();

    internal Collector(string name, string help, StringSequence instanceLabelNames, LabelSequence staticLabels, bool suppressInitialValue, ExemplarBehavior exemplarBehavior)
        : base(name, help, instanceLabelNames, staticLabels)
    {
        _suppressInitialValue = suppressInitialValue;
        _exemplarBehavior = exemplarBehavior;

        _unlabelledLazy = GetUnlabelledLazyInitializer();

        _createdLabelledChildFunc = CreateLabelledChild;
    }

    /// <summary>
    /// Creates a new instance of the child collector type.
    /// </summary>
    private protected abstract TChild NewChild(LabelSequence instanceLabels, LabelSequence flattenedLabels, bool publish, ExemplarBehavior exemplarBehavior);

#if NET
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
#endif
    internal override async ValueTask CollectAndSerializeAsync(IMetricsSerializer serializer, bool writeFamilyDeclaration, CancellationToken cancel)
    {
        EnsureUnlabelledMetricCreatedIfNoLabels();

        // There may be multiple Collectors emitting data for the same family. Only the first will write out the family declaration.
        if (writeFamilyDeclaration)
            await serializer.WriteFamilyDeclarationAsync(Name, NameBytes, HelpBytes, Type, TypeBytes, cancel);

        // This could potentially take nontrivial time, as we are serializing to a stream (potentially, a network stream).
        // Therefore we operate on a defensive copy in a reused buffer.
        TChild[] buffer;

        _childrenLock.EnterReadLock();

        var childCount = _children.Count;
        buffer = ArrayPool<TChild>.Shared.Rent(childCount);

        try
        {
            try
            {
                _children.Values.CopyTo(buffer, 0);
            }
            finally
            {
                _childrenLock.ExitReadLock();
            }

            for (var i = 0; i < childCount; i++)
            {
                var child = buffer[i];
                await child.CollectAndSerializeAsync(serializer, cancel);
            }
        }
        finally
        {
            ArrayPool<TChild>.Shared.Return(buffer, clearArray: true);
        }
    }

    private readonly bool _suppressInitialValue;

    private void EnsureUnlabelledMetricCreatedIfNoLabels()
    {
        // We want metrics to exist even with 0 values if they are supposed to be used without labels.
        // Labelled metrics are created when label values are assigned. However, as unlabelled metrics are lazy-created
        // (they are optional if labels are used) we might lose them for cases where they really are desired.

        // If there are no label names then clearly this metric is supposed to be used unlabelled, so create it.
        // Otherwise, we allow unlabelled metrics to be used if the user explicitly does it but omit them by default.
        if (!_unlabelledLazy.IsValueCreated && InstanceLabelNames.Length == 0)
            GetOrAddLabelled(LabelSequence.Empty);
    }

    private readonly ExemplarBehavior _exemplarBehavior;
}