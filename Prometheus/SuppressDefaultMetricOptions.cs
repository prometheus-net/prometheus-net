namespace Prometheus;

public sealed class SuppressDefaultMetricOptions
{
    internal static readonly SuppressDefaultMetricOptions SuppressAll = new()
    {
        SuppressProcessMetrics = true,
        SuppressDebugMetrics = true,
#if NET
        SuppressEventCounters = true,
#endif

#if NET6_0_OR_GREATER
        SuppressMeters = true
#endif
    };

    internal static readonly SuppressDefaultMetricOptions SuppressNone = new()
    {
        SuppressProcessMetrics = false,
        SuppressDebugMetrics = false,
#if NET
        SuppressEventCounters = false,
#endif

#if NET6_0_OR_GREATER
        SuppressMeters = false
#endif
    };

    /// <summary>
    /// Suppress the current-process-inspecting metrics (uptime, resource use, ...).
    /// </summary>
    public bool SuppressProcessMetrics { get; set; }

    /// <summary>
    /// Suppress metrics that prometheus-net uses to report debug information about itself (e.g. number of metrics exported).
    /// </summary>
    public bool SuppressDebugMetrics { get; set; }

#if NET
    /// <summary>
    /// Suppress the default .NET Event Counter integration.
    /// </summary>
    public bool SuppressEventCounters { get; set; }
#endif

#if NET6_0_OR_GREATER
    /// <summary>
    /// Suppress the .NET Meter API integration.
    /// </summary>
    public bool SuppressMeters { get; set; }
#endif

    internal sealed class ConfigurationCallbacks
    {
#if NET
        public Action<EventCounterAdapterOptions> ConfigureEventCounterAdapter = delegate { };
#endif

#if NET6_0_OR_GREATER
        public Action<MeterAdapterOptions> ConfigureMeterAdapter = delegate { };
#endif
    }

    /// <summary>
    /// Configures the default metrics registry based on the requested defaults behavior.
    /// </summary>
    internal void ApplyToDefaultRegistry(ConfigurationCallbacks configurationCallbacks)
    {
        if (!SuppressProcessMetrics)
            DotNetStats.RegisterDefault();

        if (!SuppressDebugMetrics)
            Metrics.DefaultRegistry.StartCollectingRegistryMetrics();

#if NET
        if (!SuppressEventCounters)
        {
            var options = new EventCounterAdapterOptions();
            configurationCallbacks.ConfigureEventCounterAdapter(options);
            EventCounterAdapter.StartListening(options);
        }
#endif

#if NET6_0_OR_GREATER
        if (!SuppressMeters)
        {
            var options = new MeterAdapterOptions();
            configurationCallbacks.ConfigureMeterAdapter(options);
            MeterAdapter.StartListening(options);
        }
#endif
    }
}
