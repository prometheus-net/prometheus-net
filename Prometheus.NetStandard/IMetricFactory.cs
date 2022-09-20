namespace Prometheus
{
    /// <summary>
    /// Allows for substitution of MetricFactory in tests.
    /// Not used by prometheus-net itself - you cannot provide your own implementation to prometheus-net code, only to your own code.
    /// </summary>
    public interface IMetricFactory
    {
        Counter CreateCounter(string name, string help, CounterConfiguration? configuration = null);
        Gauge CreateGauge(string name, string help, GaugeConfiguration? configuration = null);
        Histogram CreateHistogram(string name, string help, HistogramConfiguration? configuration = null);
        Summary CreateSummary(string name, string help, SummaryConfiguration? configuration = null);

        /// <summary>
        /// Returns a new metric factory that will add the specified labels to any metrics created using it.
        /// </summary>
        IMetricFactory WithLabels(IDictionary<string, string> labels);
    }
}
