using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Prometheus
{
    /// <summary>
    /// Base class for metrics, defining the basic informative API and the internal API.
    /// </summary>
    public abstract class Collector
    {
        /// <summary>
        /// The metric name, e.g. http_requests_total.
        /// </summary>
        public string Name { get; }
        
        internal byte[] NameBytes { get;  }

        /// <summary>
        /// The help text describing the metric for a human audience.
        /// </summary>
        public string Help { get; }

        internal byte[] HelpBytes { get;  }

        /// <summary>
        /// Names of the instance-specific labels (name-value pairs) that apply to this metric.
        /// When the values are added to the names, you get a <see cref="ChildBase"/> instance.
        /// Does not include any static label names (from metric configuration, factory or registry).
        /// </summary>
        public string[] LabelNames => _instanceLabelNamesAsArrayLazy.Value;

        internal StringSequence InstanceLabelNames;
        internal StringSequence FlattenedLabelNames;

        /// <summary>
        /// All static labels obtained from any hierarchy level (either defined in metric configuration or in registry).
        /// These will be merged with the instance-specific labels to arrive at the final flattened label sequence for a specific child.
        /// </summary>
        internal LabelSequence StaticLabels;

        internal abstract MetricType Type { get; }
        
        internal byte[] TypeBytes { get;  }

        internal abstract int ChildCount { get; }
        internal abstract int TimeseriesCount { get; }

        internal abstract Task CollectAndSerializeAsync(IMetricsSerializer serializer, CancellationToken cancel);

        // Used by ChildBase.Remove()
        internal abstract void RemoveLabelled(LabelSequence instanceLabels);

        private const string ValidMetricNameExpression = "^[a-zA-Z_][a-zA-Z0-9_]*$";
        private const string ValidLabelNameExpression = "^[a-zA-Z_][a-zA-Z0-9_]*$";
        private const string ReservedLabelNameExpression = "^__.*$";

        private static readonly Regex MetricNameRegex = new Regex(ValidMetricNameExpression, RegexOptions.Compiled);
        private static readonly Regex LabelNameRegex = new Regex(ValidLabelNameExpression, RegexOptions.Compiled);
        private static readonly Regex ReservedLabelRegex = new Regex(ReservedLabelNameExpression, RegexOptions.Compiled);

        internal Collector(string name, string help, StringSequence instanceLabelNames, LabelSequence staticLabels)
        {
            if (!MetricNameRegex.IsMatch(name))
                throw new ArgumentException($"Metric name '{name}' does not match regex '{ValidMetricNameExpression}'.");
            
            Name = name;
            NameBytes = PrometheusConstants.ExportEncoding.GetBytes(Name);
            TypeBytes = PrometheusConstants.ExportEncoding.GetBytes(Type.ToString().ToLowerInvariant());
            Help = help;
            HelpBytes = String.IsNullOrWhiteSpace(help)
                ? Array.Empty<byte>()
                : PrometheusConstants.ExportEncoding.GetBytes(help);
            InstanceLabelNames = instanceLabelNames;
            StaticLabels = staticLabels;

            FlattenedLabelNames = instanceLabelNames.Concat(staticLabels.Names);

            // Used to check uniqueness.
            var uniqueLabelNames = new HashSet<string>(StringComparer.Ordinal);

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

            _instanceLabelNamesAsArrayLazy = new Lazy<string[]>(GetInstanceLabelNamesAsStringArray);
        }

        private readonly Lazy<string[]> _instanceLabelNamesAsArrayLazy;

        private string[] GetInstanceLabelNamesAsStringArray()
        {
            return InstanceLabelNames.ToArray();
        }

        internal static void ValidateLabelName(string labelName)
        {
            if (!LabelNameRegex.IsMatch(labelName))
                throw new ArgumentException($"Label name '{labelName}' does not match regex '{ValidLabelNameExpression}'.");

            if (ReservedLabelRegex.IsMatch(labelName))
                throw new ArgumentException($"Label name '{labelName}' is not valid - labels starting with double underscore are reserved!");
        }
    }

    /// <summary>
    /// Base class for metrics collectors, providing common labeled child management functionality.
    /// </summary>
    public abstract class Collector<TChild> : Collector, ICollector<TChild>
        where TChild : ChildBase
    {
        // Keyed by the instance labels (not by flattened labels!).
        private readonly ConcurrentDictionary<LabelSequence, TChild> _labelledMetrics = new();

        // Lazy-initialized since not every collector will use a child with no labels.
        // Lazy instance will be replaced if the unlabelled timeseries is unpublished.
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
            _labelledMetrics.TryRemove(labels, out _);

            if (labels.Length == 0)
            {
                // If we remove the unlabeled instance (technically legitimate, to unpublish it) then
                // we need to also ensure that the special-casing used for it gets properly wired up the next time.
                _unlabelledLazy = GetUnlabelledLazyInitializer();
            }
        }

        private Lazy<TChild> GetUnlabelledLazyInitializer()
        {
            return new Lazy<TChild>(() => GetOrAddLabelled(LabelSequence.Empty));
        }

        internal override int ChildCount => _labelledMetrics.Count;

        /// <summary>
        /// Gets the instance-specific label values of all labelled instances of the collector.
        /// Values of any inherited static labels are not returned in the result.
        /// 
        /// Note that during concurrent operation, the set of values returned here
        /// may diverge from the latest set of values used by the collector.
        /// </summary>
        public IEnumerable<string[]> GetAllLabelValues()
        {
            foreach (var labels in _labelledMetrics.Keys)
            {
                if (labels.Length == 0)
                    continue; // We do not return the "unlabelled" label set.

                // Defensive copy.
                yield return labels.Values.ToArray();
            }
        }

        private TChild GetOrAddLabelled(LabelSequence instanceLabels)
        {
            // NOTE: We do not try to find a metric instance with the same set of label names but in a DIFFERENT order.
            // Order of labels matterns in data creation, although does not matter when the exported data set is imported later.
            // If we somehow end up registering the same metric with the same label names in different order, we will publish it twice, in two orders...
            // That is not ideal but also not that big of a deal to do a lookup every time a metric instance is registered.

            // Don't allocate lambda for GetOrAdd in the common case that the labeled metrics exist.
            if (_labelledMetrics.TryGetValue(instanceLabels, out var metric))
                return metric;

            return _labelledMetrics.GetOrAdd(instanceLabels, CreateLabelledChild);
        }

        private TChild CreateLabelledChild(LabelSequence instanceLabels)
        {
            // Order of labels is 1) instance labels; 2) static labels.
            var flattenedLabels = instanceLabels.Concat(StaticLabels);

            return NewChild(instanceLabels, flattenedLabels, publish: !_suppressInitialValue);
        }

        /// <summary>
        /// For tests that want to see what instance-level label values were used when metrics were created.
        /// </summary>
        internal LabelSequence[] GetAllInstanceLabels() => _labelledMetrics.Select(p => p.Key).ToArray();

        internal Collector(string name, string help, StringSequence instanceLabelNames, LabelSequence staticLabels, bool suppressInitialValue)
            : base(name, help, instanceLabelNames, staticLabels)
        {
            _suppressInitialValue = suppressInitialValue;

            _unlabelledLazy = GetUnlabelledLazyInitializer();

            _familyHeaderLines = new byte[][]
            {
                string.IsNullOrWhiteSpace(help)
                    ? PrometheusConstants.ExportEncoding.GetBytes($"# HELP {name}")
                    : PrometheusConstants.ExportEncoding.GetBytes($"# HELP {name} {help}"),
                PrometheusConstants.ExportEncoding.GetBytes($"# TYPE {name} {Type.ToString().ToLowerInvariant()}")
            };
        }

        /// <summary>
        /// Creates a new instance of the child collector type.
        /// </summary>
        private protected abstract TChild NewChild(LabelSequence instanceLabels, LabelSequence flattenedLabels, bool publish);

        private readonly byte[][] _familyHeaderLines;

        internal override async Task CollectAndSerializeAsync(IMetricsSerializer serializer, CancellationToken cancel)
        {
            EnsureUnlabelledMetricCreatedIfNoLabels();

            await serializer.WriteFamilyDeclarationAsync(Name, NameBytes, HelpBytes, Type, TypeBytes, cancel);

            foreach (var child in _labelledMetrics.Values)
                await child.CollectAndSerializeAsync(serializer, cancel);
        }

        private readonly bool _suppressInitialValue;

        private void EnsureUnlabelledMetricCreatedIfNoLabels()
        {
            // We want metrics to exist even with 0 values if they are supposed to be used without labels.
            // Labelled metrics are created when label values are assigned. However, as unlabelled metrics are lazy-created
            // (they are optional if labels are used) we might lose them for cases where they really are desired.

            // If there are no label names then clearly this metric is supposed to be used unlabelled, so create it.
            // Otherwise, we allow unlabelled metrics to be used if the user explicitly does it but omit them by default.
            if (!_unlabelledLazy.IsValueCreated && !LabelNames.Any())
                GetOrAddLabelled(LabelSequence.Empty);
        }
    }
}