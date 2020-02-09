using System;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus
{
    /// <summary>
    /// Base class for labeled instances of metrics (with all label names and label values defined).
    /// </summary>
    public abstract class ChildBase : ICollectorChild, IDisposable
    {
        internal ChildBase(Collector parent, Labels labels, bool publish)
        {
            _parent = parent;
            _labels = labels;
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
            _parent.RemoveLabelled(_labels);
        }

        public void Dispose() => Remove();

        private readonly Collector _parent;
        private readonly Labels _labels;

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
        /// Creates a metric identifier, with an optional name postfix and optional extra labels.
        /// familyname_postfix{labelkey1="labelvalue1",labelkey2="labelvalue2"}
        /// </summary>
        protected byte[] CreateIdentifier(string? postfix = null, params (string, string)[] extraLabels)
        {
            var fullName = postfix != null ? $"{_parent.Name}_{postfix}" : _parent.Name;

            var labels = _labels;
            if (extraLabels?.Length > 0)
                labels = _labels.Concat(extraLabels);

            if (labels.Count != 0)
                return PrometheusConstants.ExportEncoding.GetBytes($"{fullName}{{{labels.Serialize()}}}");
            else
                return PrometheusConstants.ExportEncoding.GetBytes(fullName);
        }
    }
}