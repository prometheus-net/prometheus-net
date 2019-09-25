namespace Prometheus
{
    /// <summary>
    /// This class packages the options for creating metrics into a single class (with subclasses per metric type)
    /// for easy extensibility of the API without adding numerous method overloads whenever new options are added.
    /// </summary>
    public abstract class MetricConfiguration
    {
        /// <summary>
        /// Names of all the label fields that are defined for the metric.
        /// If null, the metric will be created without any labels.
        /// 
        /// Before using a metric that uses labels, .WithLabels() must be called to provide values for the labels.
        /// </summary>
        public string[]? LabelNames { get; set; }

        /// <summary>
        /// If true, the metric will not be published until its value is first modified (regardless of the specific value).
        /// This is useful to delay publishing gauges that get their initial values delay-loaded.
        /// 
        /// By default, metrics are published as soon as possible - if they do not use labels then they are published on
        /// creation and if they use labels then as soon as the label values are assigned.
        /// </summary>
        public bool SuppressInitialValue { get; set; }
    }
}
