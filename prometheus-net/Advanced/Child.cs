using Prometheus.Advanced.DataContracts;
using Prometheus.Internal;
using System;
using System.Threading;

namespace Prometheus.Advanced
{
    public abstract class Child
    {
        private LabelValues _labelValues;

        // If 0, no timestamp is reported (Prometheus will use current time).
        private long _timestamp;

        internal virtual void Init(ICollector parent, LabelValues labelValues)
        {
            _labelValues = labelValues;
        }

        /// <summary>
        /// Sets the timestamp that Prometheus should use when recording this metric.
        /// If null, Prometheus will use the current time.
        /// </summary>
        public void SetTimestamp(DateTimeOffset? timestamp)
        {
            if (timestamp == null)
            {
                Interlocked.Exchange(ref _timestamp, 0);
            }
            else
            {
                // Conversion copied from DateTimeOffset implementation for pre-4.6 compatibility.
                var timestampAsLong = (timestamp.Value.UtcDateTime.Ticks - 0x89f7ff5f7b58000L) / 10000;
                Interlocked.Exchange(ref _timestamp, timestampAsLong);
            }
        }

        protected abstract void Populate(Metric metric);

        internal Metric Collect()
        {
            var metric = new Metric();
            Populate(metric);
            metric.label = _labelValues.WireLabels;
            metric.timestamp_ms = Interlocked.Read(ref _timestamp);

            return metric;
        }
    }
}