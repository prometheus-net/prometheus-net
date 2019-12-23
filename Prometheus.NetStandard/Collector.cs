using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

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

        /// <summary>
        /// The help text describing the metric for a human audience.
        /// </summary>
        public string Help { get; }

        /// <summary>
        /// Names of the labels (name-value pairs) that apply to this metric.
        /// When the values are added to the names, you get a <see cref="ChildBase"/> instance.
        /// </summary>
        public string[] LabelNames { get; }

        internal abstract Task CollectAndSerializeAsync(IMetricsSerializer serializer, CancellationToken cancel);

        // Used by ChildBase.Remove()
        internal abstract void RemoveLabelled(Labels labels);

        private static readonly string[] EmptyLabelNames = new string[0];

        private const string ValidMetricNameExpression = "^[a-zA-Z_:][a-zA-Z0-9_:]*$";
        private const string ValidLabelNameExpression = "^[a-zA-Z_:][a-zA-Z0-9_:]*$";
        private const string ReservedLabelNameExpression = "^__.*$";

        private static readonly Regex MetricNameRegex = new Regex(ValidMetricNameExpression, RegexOptions.Compiled);
        private static readonly Regex LabelNameRegex = new Regex(ValidLabelNameExpression, RegexOptions.Compiled);
        private static readonly Regex ReservedLabelRegex = new Regex(ReservedLabelNameExpression, RegexOptions.Compiled);

        protected Collector(string name, string help, string[]? labelNames)
        {
            labelNames ??= EmptyLabelNames;

            if (!MetricNameRegex.IsMatch(name))
                throw new ArgumentException($"Metric name '{name}' does not match regex '{ValidMetricNameExpression}'.");

            foreach (var labelName in labelNames)
            {
                if (labelName == null)
                    throw new ArgumentNullException("Label name was null.");

                if (!LabelNameRegex.IsMatch(labelName))
                    throw new ArgumentException($"Label name '{labelName}' does not match regex '{ValidLabelNameExpression}'.");

                if (ReservedLabelRegex.IsMatch(labelName))
                    throw new ArgumentException($"Label name '{labelName}' is not valid - labels starting with double underscore are reserved!");
            }

            Name = name;
            Help = help;
            LabelNames = labelNames;
        }
    }

    /// <summary>
    /// Base class for metrics collectors, providing common labeled child management functionality.
    /// </summary>
    public abstract class Collector<TChild> : Collector, ICollector<TChild>
        where TChild : ChildBase
    {
        private readonly ConcurrentDictionary<Labels, TChild> _labelledMetrics = new ConcurrentDictionary<Labels, TChild>();

        // Lazy-initialized since not every collector will use a child with no labels.
        private readonly Lazy<TChild> _unlabelledLazy;

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
            var key = new Labels(LabelNames, labelValues);
            return GetOrAddLabelled(key);
        }

        public void RemoveLabelled(params string[] labelValues)
        {
            var key = new Labels(LabelNames, labelValues);
            _labelledMetrics.TryRemove(key, out _);
        }

        internal override void RemoveLabelled(Labels labels)
        {
            _labelledMetrics.TryRemove(labels, out _);
        }

        /// <summary>
        /// Gets the label values of all labelled instances of the collector.
        /// 
        /// Note that during concurrent operation, the set of values returned here
        /// may diverge from the latest set of values used by the collector.
        /// </summary>
        public IEnumerable<string[]> GetAllLabelValues()
        {
            foreach (var labels in _labelledMetrics.Keys)
            {
                if (labels.Count == 0)
                    continue; // We do not return the "unlabelled" label set.

                // Defensive copy.
                yield return labels.Values.ToArray();
            }
        }

        private TChild GetOrAddLabelled(Labels key)
        {
            return _labelledMetrics.GetOrAdd(key, k => NewChild(k, publish: !_suppressInitialValue));
        }

        /// <summary>
        /// For tests that want to see what label values were used when metrics were created.
        /// </summary>
        internal Labels[] GetAllLabels() => _labelledMetrics.Select(p => p.Key).ToArray();

        protected Collector(string name, string help, string[]? labelNames, bool suppressInitialValue)
            : base(name, help, labelNames)
        {
            _suppressInitialValue = suppressInitialValue;
            _unlabelledLazy = new Lazy<TChild>(() => GetOrAddLabelled(Prometheus.Labels.Empty));

            _familyHeaderLines = new byte[][]
            {
                PrometheusConstants.ExportEncoding.GetBytes($"# HELP {name} {help}"),
                PrometheusConstants.ExportEncoding.GetBytes($"# TYPE {name} {Type.ToString().ToLowerInvariant()}")
            };
        }

        /// <summary>
        /// Creates a new instance of the child collector type.
        /// </summary>
        private protected abstract TChild NewChild(Labels labels, bool publish);

        private protected abstract MetricType Type { get; }

        private readonly byte[][] _familyHeaderLines;

        internal override async Task CollectAndSerializeAsync(IMetricsSerializer serializer, CancellationToken cancel)
        {
            EnsureUnlabelledMetricCreatedIfNoLabels();

            await serializer.WriteFamilyDeclarationAsync(_familyHeaderLines, cancel);

            foreach (var child in _labelledMetrics.Values)
                await child.CollectAndSerializeAsync(serializer, cancel);
        }

        private readonly bool _suppressInitialValue;

        private void EnsureUnlabelledMetricCreatedIfNoLabels()
        {
            // We want metrics to exist even with 0 values if they are supposed to be used without labels.
            // Labelled metrics are created when label values are assigned. However, as unlabelled metrics are lazy-created
            // (they might are optional if labels are used) we might lose them for cases where they really are desired.

            // If there are no label names then clearly this metric is supposed to be used unlabelled, so create it.
            // Otherwise, we allow unlabelled metrics to be used if the user explicitly does it but omit them by default.
            if (!_unlabelledLazy.IsValueCreated && !LabelNames.Any())
                GetOrAddLabelled(Prometheus.Labels.Empty);
        }
    }
}