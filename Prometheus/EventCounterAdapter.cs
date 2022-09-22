using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

namespace Prometheus
{
    /// <summary>
    /// Monitors all .NET EventCounters and exposes them as Prometheus counters and gauges.
    /// </summary>
    /// <remarks>
    /// Rate-based .NET event counters are transformed into Prometheus gauges indicating count per escond.
    /// Incrementing .NET event counters are transformed into Prometheus counters.
    /// </remarks>
    public sealed class EventCounterAdapter : IDisposable
    {
        public static IDisposable StartListening() => new EventCounterAdapter(EventCounterAdapterOptions.Default);

        public static IDisposable StartListening(EventCounterAdapterOptions options) => new EventCounterAdapter(options);

        private EventCounterAdapter(EventCounterAdapterOptions options)
        {
            _options = options;
            _metricFactory = Metrics.WithCustomRegistry(_options.Registry);

            _gauge = _metricFactory.CreateGauge("dotnet_gauge", "Values from .NET aggregating EventCounters (count per second).", new GaugeConfiguration
            {
                LabelNames = new[] { "source", "name", "display_name" }
            });
            _counter = _metricFactory.CreateCounter("dotnet_counter", "Values from .NET incrementing EventCounters.", new CounterConfiguration
            {
                LabelNames = new[] { "source", "name", "display_name" }
            });

            _listener = new Listener(OnEventSourceCreated, OnEventWritten);
        }

        public void Dispose()
        {
            // Disposal means we stop listening but we do not remove any published data just to keep things simple.
            _listener.Dispose();
        }

        private readonly EventCounterAdapterOptions _options;
        private readonly IMetricFactory _metricFactory;

        // Each event counter is published either in gauge or counter form,
        // depending on the type of the native .NET event counter (incrementing or aggregating).,
        private readonly Gauge _gauge;
        private readonly Counter _counter;

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

                // The event counter can either be
                // 1) an aggregating counter (in which case we use the mean); or
                // 2) an incrementing counter (in which case we use the delta).

                if (e.TryGetValue("Increment", out var increment))
                {
                    // Looks like an incrementing counter.

                    var value = increment as double?;

                    if (value == null)
                        continue; // What? Whatever.

                    _counter.WithLabels(eventSourceName, name, displayName).Inc(value.Value);
                }
                else if (e.TryGetValue("Mean", out var mean))
                {
                    // Looks like an aggregating counter.

                    var value = mean as double?;

                    if (value == null)
                        continue; // What? Whatever.

                    _gauge.WithLabels(eventSourceName, name, displayName).Set(value.Value);
                }
                else
                {
                    Thread.Sleep(0);
                }
            }
        }

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
