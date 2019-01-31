using System.Collections.Generic;

namespace Prometheus
{
    internal sealed class LabelPairData
    {
        public string Name;
        public string Value;
    }

    internal sealed class GaugeData
    {
        public double Value;
    }

    internal sealed class CounterData
    {
        public double Value;
    }

    internal sealed class SummaryData
    {
        public long SampleCount;
        public double SampleSum;
        public SummaryQuantileData[] Quantiles;
    }

    internal sealed class SummaryQuantileData
    {
        public double Quantile;
        public double Value;
    }

    internal sealed class HistogramData
    {
        public long SampleCount;
        public double SampleSum;
        public HistogramBucketData[] Buckets;
    }

    internal sealed class HistogramBucketData
    {
        public long CumulativeCount;
        public double UpperBound;
    }

    internal sealed class MetricData
    {
        public LabelPairData[] Labels;
        public GaugeData Gauge;
        public CounterData Counter;
        public SummaryData Summary;
        public HistogramData Histogram;
    }

    internal sealed class MetricFamilyData
    {
        public string Name;
        public string Help;
        public MetricType Type;
        public List<MetricData> Metrics;
    }

    internal enum MetricType
    {
        Counter,
        Gauge,
        Summary,
        Histogram
    }
}
