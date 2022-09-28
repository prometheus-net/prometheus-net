namespace Prometheus
{
    public sealed class SuppressDefaultMetricOptions
    {
        internal static readonly SuppressDefaultMetricOptions SuppressAll = new()
        {
            SuppressProcessMetrics = true,
            SuppressDebugMetrics = true,
#if NET6_0_OR_GREATER
            SuppressEventCounters = true,
            SuppressMeters = true
#endif
        };

        internal static readonly SuppressDefaultMetricOptions SuppressNone = new()
        {
            SuppressProcessMetrics = false,
            SuppressDebugMetrics = false,
#if NET6_0_OR_GREATER
            SuppressEventCounters = false,
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

#if NET6_0_OR_GREATER
        /// <summary>
        /// Suppress the default .NET Event Counter integration.
        /// </summary>
        public bool SuppressEventCounters { get; set; }

        /// <summary>
        /// Suppress the .NET Meter API integration.
        /// </summary>
        public bool SuppressMeters { get; set; }
#endif

        internal sealed class ConfigurationCallbacks
        {
#if NET6_0_OR_GREATER
            public Action<EventCounterAdapterOptions> ConfigureEventCounterAdapter = delegate { };
            public Action<MeterAdapterOptions> ConfigureMeterAdapter = delegate { };
#endif
        }

        /// <summary>
        /// Configures the target registry based on the requested defaults behavior.
        /// </summary>
        internal void Configure(CollectorRegistry registry, ConfigurationCallbacks configurationCallbacks)
        {
            // We include some metrics by default, just to give some output when a user first uses the library.
            // These are not designed to be super meaningful/useful metrics.
            if (!SuppressProcessMetrics)
                DotNetStats.Register(registry);

            if (!SuppressDebugMetrics)
                registry.StartCollectingRegistryMetrics();

#if NET6_0_OR_GREATER
            if (!SuppressEventCounters)
            {
                var options = new EventCounterAdapterOptions();
                configurationCallbacks.ConfigureEventCounterAdapter(options);
                EventCounterAdapter.StartListening(options);
            }

            if (!SuppressMeters)
            {
                var options = new MeterAdapterOptions();
                configurationCallbacks.ConfigureMeterAdapter(options);
                MeterAdapter.StartListening(options);
            }
#endif
        }
    }
}
