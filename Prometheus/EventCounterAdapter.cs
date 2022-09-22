using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Runtime.InteropServices;

namespace Prometheus
{
    /// <summary>
    /// Monitors all .NET EventCounters and exposes them as Prometheus metrics.
    /// </summary>
    /// <remarks>
    /// All .NET event counters are transformed into Prometheus metrics with translated names.
    /// 
    /// There appear to be different types of "incrementing" counters in .NET:
    /// * some "incrementing" counters publish the increment value ("+5")
    /// * some "incrementing" counters are just gauges that publish a "current" value
    /// 
    /// It is not possible for us to really determine which is which, so for incrementing event counters we publish both a gauge (with latest value) and counter (with total value).
    /// Which one you use for which .NET event counter depends on how the authors of the event counter made it - one of them will be wrong!
    /// </remarks>
    public sealed class EventCounterAdapter : IDisposable
    {
        public static IDisposable StartListening() => new EventCounterAdapter(EventCounterAdapterOptions.Default);

        public static IDisposable StartListening(EventCounterAdapterOptions options) => new EventCounterAdapter(options);

        private EventCounterAdapter(EventCounterAdapterOptions options)
        {
            _options = options;
            _metricFactory = Metrics.WithCustomRegistry(_options.Registry);

            _listener = new Listener(OnEventSourceCreated, OnEventWritten);
        }

        public void Dispose()
        {
            // Disposal means we stop listening but we do not remove any published data just to keep things simple.
            _listener.Dispose();
        }

        private readonly EventCounterAdapterOptions _options;
        private readonly IMetricFactory _metricFactory;

        private readonly Listener _listener;

        private bool OnEventSourceCreated(EventSource source)
        {
            return _options.EventSourceFilterPredicate(source.Name);
        }

        private void OnEventWritten(EventWrittenEventArgs args)
        {
            // This deserialization here is pretty gnarly.
            // We just skip anything that makes no sense.

            if (args.EventName != "EventCounters")
                return; // Do not know what it is and do not care.

            if (args.Payload == null)
                return; // What? Whatever.

            var eventSourceName = args.EventSource.Name;

            foreach (var item in args.Payload)
            {
                if (item is not IDictionary<string, object> e)
                    continue;

                if (!e.TryGetValue("Name", out var nameWrapper))
                    continue;

                var name = nameWrapper as string;

                if (name == null)
                    continue; // What? Whatever.

                if (!e.TryGetValue("DisplayName", out var displayNameWrapper))
                    continue;

                var displayName = displayNameWrapper as string ?? "";

                // If there is a DisplayUnits, prefix it to the help text.
                if (e.TryGetValue("DisplayUnits", out var displayUnitsWrapper) && !string.IsNullOrWhiteSpace(displayUnitsWrapper as string))
                    displayName = $"({(string)displayUnitsWrapper}) {displayName}";

                var mergedName = $"{eventSourceName}_{name}";
                
                var prometheusName = _counterPrometheusName.GetOrAdd(mergedName, PrometheusNameHelpers.TranslateNameToPrometheusName);

                // The event counter can either be
                // 1) an aggregating counter (in which case we use the mean); or
                // 2) an incrementing counter (in which case we use the delta, which might not actually be the delta).

                if (e.TryGetValue("Increment", out var increment))
                {
                    // Looks like an incrementing counter.

                    var value = increment as double?;

                    if (value == null)
                        continue; // What? Whatever.

                    // It seems there exist "incrementing" event counters that behave as both counters and gauges.
                    // We must leave it up to the user to figure out which event counter is which.
                    _metricFactory.CreateGauge(prometheusName, displayName).Set(value.Value);
                    _metricFactory.CreateCounter(prometheusName + "_total", displayName).Inc(value.Value);
                }
                else if (e.TryGetValue("Mean", out var mean))
                {
                    // Looks like an aggregating counter.

                    var value = mean as double?;

                    if (value == null)
                        continue; // What? Whatever.

                    _metricFactory.CreateGauge(prometheusName, displayName).Set(value.Value);
                }
            }
        }

        // Source+Name -> Name
        private readonly ConcurrentDictionary<string, string> _counterPrometheusName = new();

        private sealed class Listener : EventListener
        {
            public Listener(Func<EventSource, bool> onEventSourceCreated, Action<EventWrittenEventArgs> onEventWritten)
            {
                _onEventSourceCreated = onEventSourceCreated;
                _onEventWritten = onEventWritten;

                foreach (var eventSource in _preRegisteredEventSources)
                    OnEventSourceCreated(eventSource);

                _preRegisteredEventSources.Clear();
            }

            private readonly List<EventSource> _preRegisteredEventSources = new List<EventSource>();

            private readonly Func<EventSource, bool> _onEventSourceCreated;
            private readonly Action<EventWrittenEventArgs> _onEventWritten;

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (_onEventSourceCreated == null)
                {
                    // The way this EventListener thing works is rather strange. Immediately in the base class constructor, before we
                    // have even had time to wire up our subclass, it starts calling OnEventSourceCreated for all already-existing event sources...
                    // We just buffer those calls because CALM DOWN SIR!
                    _preRegisteredEventSources.Add(eventSource);
                    return;
                }

                if (!_onEventSourceCreated(eventSource))
                    return;

                EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All, new Dictionary<string, string?>()
                {
                    ["EventCounterIntervalSec"] = "1"
                });
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                _onEventWritten(eventData);
            }
        }
    }
}
