namespace Prometheus
{
    /// <summary>
    /// Static class for easy creation of metrics. Acts as the entry point to the prometheus-net metrics recording API.
    /// 
    /// Some built-in metrics are registered by default in the default collector registry. This is mainly to ensure that
    /// the library exports some metrics when installed. If these default metrics are not desired, call
    /// <see cref="SuppressDefaultMetrics"/> to remove them before registering your own.
    /// </summary>
    public static class Metrics
    {
        /// <summary>
        /// The default registry where all metrics are registered by default.
        /// </summary>
        public static CollectorRegistry DefaultRegistry { get; private set; }

        private static MetricFactory _defaultFactory;

        /// <summary>
        /// Creates a new registry. You may want to use multiple registries if you want to
        /// export different sets of metrics via different exporters (e.g. on different URLs).
        /// </summary>
        public static CollectorRegistry NewCustomRegistry() => new CollectorRegistry();

        /// <summary>
        /// Returns an instance of <see cref="MetricFactory" /> that you can use to register metrics in a custom registry.
        /// </summary>
        public static MetricFactory WithCustomRegistry(CollectorRegistry registry) =>
            new MetricFactory(registry);

        /// <summary>
        /// Counters only increase in value and reset to zero when the process restarts.
        /// </summary>
        public static Counter CreateCounter(string name, string help, CounterConfiguration configuration = null) =>
            _defaultFactory.CreateCounter(name, help, configuration);

        /// <summary>
        /// Gauges can have any numeric value and change arbitrarily.
        /// </summary>
        public static Gauge CreateGauge(string name, string help, GaugeConfiguration configuration = null) =>
            _defaultFactory.CreateGauge(name, help, configuration);

        /// <summary>
        /// Summaries track the trends in events over time (10 minutes by default).
        /// </summary>
        public static Summary CreateSummary(string name, string help, SummaryConfiguration configuration = null) =>
            _defaultFactory.CreateSummary(name, help, configuration);

        /// <summary>
        /// Histograms track the size and number of events in buckets.
        /// </summary>
        public static Histogram CreateHistogram(string name, string help, HistogramConfiguration configuration = null) =>
            _defaultFactory.CreateHistogram(name, help, configuration);

        /// <summary>
        /// Counters only increase in value and reset to zero when the process restarts.
        /// </summary>
        public static Counter CreateCounter(string name, string help, params string[] labelNames) =>
            _defaultFactory.CreateCounter(name, help, labelNames);

        /// <summary>
        /// Gauges can have any numeric value and change arbitrarily.
        /// </summary>
        public static Gauge CreateGauge(string name, string help, params string[] labelNames) =>
            _defaultFactory.CreateGauge(name, help, labelNames);

        /// <summary>
        /// Summaries track the trends in events over time (10 minutes by default).
        /// </summary>
        public static Summary CreateSummary(string name, string help, params string[] labelNames) =>
            _defaultFactory.CreateSummary(name, help, labelNames);

        /// <summary>
        /// Histograms track the size and number of events in buckets.
        /// </summary>
        public static Histogram CreateHistogram(string name, string help, params string[] labelNames) =>
            _defaultFactory.CreateHistogram(name, help, labelNames);

        static Metrics()
        {
            NewDefaultRegistry();

            DotNetStats.Register(DefaultRegistry);
        }

        /// <summary>
        /// Replaces the default registry with a completely empty registry.
        /// This will remove all registered metrics, so call this before doing any of your own registrations.
        /// </summary>
        public static void SuppressDefaultMetrics()
        {
            // This should only be called before ever using any of the CreateXYZ() methods.

            NewDefaultRegistry();
        }

        private static void NewDefaultRegistry()
        {
            DefaultRegistry = new CollectorRegistry();
            _defaultFactory = new MetricFactory(DefaultRegistry);
        }
    }
}