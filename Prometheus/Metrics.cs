namespace Prometheus;

/// <summary>
/// Static class for easy creation of metrics. Acts as the entry point to the prometheus-net metrics recording API.
/// 
/// Some built-in metrics are registered by default in the default collector registry. If these default metrics are
/// not desired, call <see cref="SuppressDefaultMetrics()"/> to remove them before registering your own.
/// </summary>
public static class Metrics
{
    /// <summary>
    /// The default registry where all metrics are registered by default.
    /// </summary>
    public static CollectorRegistry DefaultRegistry { get; private set; }
    
    /// <summary>
    /// The default metric factory used to create collectors in the default registry.
    /// </summary>
    public static MetricFactory DefaultFactory { get; private set; }

    /// <summary>
    /// Creates a new registry. You may want to use multiple registries if you want to
    /// export different sets of metrics via different exporters (e.g. on different URLs).
    /// </summary>
    public static CollectorRegistry NewCustomRegistry() => new();

    /// <summary>
    /// Returns an instance of <see cref="MetricFactory" /> that you can use to register metrics in a custom registry.
    /// </summary>
    public static MetricFactory WithCustomRegistry(CollectorRegistry registry) => new(registry);

    /// <summary>
    /// Adds the specified static labels to all metrics created using the returned factory.
    /// </summary>
    public static IMetricFactory WithLabels(IDictionary<string, string> labels) =>
        new MetricFactory(DefaultRegistry, LabelSequence.From(labels));

    /// <summary>
    /// Returns a factory that creates metrics with a managed lifetime.
    /// </summary>
    /// <param name="expiresAfter">
    /// Metrics created from this factory will expire after this time span elapses, enabling automatic deletion of unused metrics.
    /// The expiration timer is reset to zero for the duration of any active lifetime-extension lease that is taken on a specific metric.
    /// </param>
    public static IManagedLifetimeMetricFactory WithManagedLifetime(TimeSpan expiresAfter) =>
        DefaultFactory.WithManagedLifetime(expiresAfter);

    /// <summary>
    /// Counters only increase in value and reset to zero when the process restarts.
    /// </summary>
    public static Counter CreateCounter(string name, string help, CounterConfiguration? configuration = null) =>
        DefaultFactory.CreateCounter(name, help, configuration);

    /// <summary>
    /// Gauges can have any numeric value and change arbitrarily.
    /// </summary>
    public static Gauge CreateGauge(string name, string help, GaugeConfiguration? configuration = null) =>
        DefaultFactory.CreateGauge(name, help, configuration);

    /// <summary>
    /// Summaries track the trends in events over time (10 minutes by default).
    /// </summary>
    public static Summary CreateSummary(string name, string help, SummaryConfiguration? configuration = null) =>
        DefaultFactory.CreateSummary(name, help, configuration);

    /// <summary>
    /// Histograms track the size and number of events in buckets.
    /// </summary>
    public static Histogram CreateHistogram(string name, string help, HistogramConfiguration? configuration = null) =>
        DefaultFactory.CreateHistogram(name, help, configuration);

    /// <summary>
    /// Counters only increase in value and reset to zero when the process restarts.
    /// </summary>
    public static Counter CreateCounter(string name, string help, string[] labelNames, CounterConfiguration? configuration = null) =>
        DefaultFactory.CreateCounter(name, help, labelNames, configuration);

    /// <summary>
    /// Gauges can have any numeric value and change arbitrarily.
    /// </summary>
    public static Gauge CreateGauge(string name, string help, string[] labelNames, GaugeConfiguration? configuration = null) =>
        DefaultFactory.CreateGauge(name, help, labelNames, configuration);

    /// <summary>
    /// Summaries track the trends in events over time (10 minutes by default).
    /// </summary>
    public static Summary CreateSummary(string name, string help, string[] labelNames, SummaryConfiguration? configuration = null) =>
        DefaultFactory.CreateSummary(name, help, labelNames, configuration);

    /// <summary>
    /// Histograms track the size and number of events in buckets.
    /// </summary>
    public static Histogram CreateHistogram(string name, string help, string[] labelNames, HistogramConfiguration? configuration = null) =>
        DefaultFactory.CreateHistogram(name, help, labelNames, configuration);

    /// <summary>
    /// Counters only increase in value and reset to zero when the process restarts.
    /// </summary>
    public static Counter CreateCounter(string name, string help, params string[] labelNames) =>
        DefaultFactory.CreateCounter(name, help, labelNames);

    /// <summary>
    /// Gauges can have any numeric value and change arbitrarily.
    /// </summary>
    public static Gauge CreateGauge(string name, string help, params string[] labelNames) =>
        DefaultFactory.CreateGauge(name, help, labelNames);

    /// <summary>
    /// Summaries track the trends in events over time (10 minutes by default).
    /// </summary>
    public static Summary CreateSummary(string name, string help, params string[] labelNames) =>
        DefaultFactory.CreateSummary(name, help, labelNames);

    /// <summary>
    /// Histograms track the size and number of events in buckets.
    /// </summary>
    public static Histogram CreateHistogram(string name, string help, params string[] labelNames) =>
        DefaultFactory.CreateHistogram(name, help, labelNames);

    static Metrics()
    {
        DefaultRegistry = new CollectorRegistry();

        // Configures defaults to their default behaviors, can be overridden by user if they desire (before first collection).
        SuppressDefaultMetrics(SuppressDefaultMetricOptions.SuppressNone);

        DefaultFactory = new MetricFactory(DefaultRegistry);
    }

    /// <summary>
    /// Suppresses the registration of the default sample metrics from the default registry.
    /// Has no effect if not called on startup (it will not remove metrics from a registry already in use).
    /// </summary>
    public static void SuppressDefaultMetrics() => SuppressDefaultMetrics(SuppressDefaultMetricOptions.SuppressAll);

    /// <summary>
    /// Suppresses the registration of the default sample metrics from the default registry.
    /// Has no effect if not called on startup (it will not remove metrics from a registry already in use).
    /// </summary>
    public static void SuppressDefaultMetrics(SuppressDefaultMetricOptions options)
    {
        options ??= SuppressDefaultMetricOptions.SuppressAll;

        // Only has effect if called before the registry is collected from. Otherwise a no-op.
        DefaultRegistry.SetBeforeFirstCollectCallback(delegate
        {
            var configureCallbacks = new SuppressDefaultMetricOptions.ConfigurationCallbacks()
            {
#if NET
                ConfigureEventCounterAdapter = _configureEventCounterAdapterCallback,
#endif
#if NET6_0_OR_GREATER
                ConfigureMeterAdapter = _configureMeterAdapterOptions
#endif
            };

            options.ApplyToDefaultRegistry(configureCallbacks);
        });
    }

#if NET
    private static Action<EventCounterAdapterOptions> _configureEventCounterAdapterCallback = delegate { };

    /// <summary>
    /// Configures the event counter adapter that is enabled by default on startup.
    /// </summary>
    public static void ConfigureEventCounterAdapter(Action<EventCounterAdapterOptions> callback) => _configureEventCounterAdapterCallback = callback;
#endif

#if NET6_0_OR_GREATER
    private static Action<MeterAdapterOptions> _configureMeterAdapterOptions = delegate { };

    /// <summary>
    /// Configures the meter adapter that is enabled by default on startup.
    /// </summary>
    public static void ConfigureMeterAdapter(Action<MeterAdapterOptions> callback) => _configureMeterAdapterOptions = callback;
#endif
}