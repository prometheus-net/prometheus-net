using System;
using Prometheus.Internal;

namespace Prometheus
{
    public class Metrics
    {
        public static Counter CreateCounter(string name, string help, params string[] labelNames)
        {
            return new Counter(MetricsRegistry.Instance.GetOrAdd(name, help, labelNames, typeof(Counter)), LabelValues.Empty);
        }

        public static Gauge CreateGauge(string name, string help, params string[] labelNames)
        {
            return new Gauge(MetricsRegistry.Instance.GetOrAdd(name, help, labelNames, typeof(Gauge)), LabelValues.Empty);
        }

        public static Summary CreateSummary(string name, string help, params string[] labelNames)
        {
            return new Summary(MetricsRegistry.Instance.GetOrAdd(name, help, labelNames, typeof(Summary)), LabelValues.Empty);
        }

        public static Histogram CreateHistogram(string name, string help, double[] buckets = null, params string[] labelNames)
        {
            return new Histogram(MetricsRegistry.Instance.GetOrAdd(name, help, labelNames, typeof(Histogram)), LabelValues.Empty, buckets);
        }
    }
}