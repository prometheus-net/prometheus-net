#if NET6_0_OR_GREATER
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Text;

namespace Prometheus;

/// <summary>
/// Publishes .NET Meters as Prometheus metrics.
/// </summary>
public sealed class MeterAdapter : IDisposable
{
    public static IDisposable StartListening() => new MeterAdapter(MeterAdapterOptions.Default);

    public static IDisposable StartListening(MeterAdapterOptions options) => new MeterAdapter(options);

    private MeterAdapter(MeterAdapterOptions options)
    {
        _options = options;

        _registry = options.Registry;
        _factory = (ManagedLifetimeMetricFactory)Metrics.WithCustomRegistry(_options.Registry)
            .WithManagedLifetime(expiresAfter: options.MetricsExpireAfter);

        _inheritedStaticLabelNames = _factory.GetAllStaticLabelNames().ToArray();

        _listener.InstrumentPublished = OnInstrumentPublished;
        _listener.MeasurementsCompleted += OnMeasurementsCompleted;
        _listener.SetMeasurementEventCallback<byte>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<short>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<int>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<long>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<float>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<double>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<decimal>(OnMeasurementRecorded);

        var regularFactory = Metrics.WithCustomRegistry(_registry);
        _instrumentsConnected = regularFactory.CreateGauge("prometheus_net_meteradapter_instruments_connected", "Number of instruments that are currently connected to the adapter.");

        _listener.Start();

        _registry.AddBeforeCollectCallback(delegate
        {
            // ICollectorRegistry does not support unregistering the callback, so we just no-op when disposed.
            // The expected pattern is that any disposal of the pipeline also throws away the ICollectorRegistry.
            if (_disposed)
                return;

            // Seems OK to call even when _listener has been disposed.
            _listener.RecordObservableInstruments();
        });
    }

    private readonly MeterAdapterOptions _options;

    private readonly CollectorRegistry _registry;
    private readonly ManagedLifetimeMetricFactory _factory;
    private readonly string[] _inheritedStaticLabelNames;

    private readonly Gauge _instrumentsConnected;

    private readonly MeterListener _listener = new MeterListener();

    private volatile bool _disposed;
    private readonly object _lock = new();

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
        }

        _listener.Dispose();
    }

    private void OnInstrumentPublished(Instrument instrument, MeterListener listener)
    {
        if (!_options.InstrumentFilterPredicate(instrument))
            return; // This instrument is not wanted.

        _instrumentsConnected.Inc();

        _instrumentPrometheusNames.TryAdd(instrument, TranslateInstrumentNameToPrometheusName(instrument));
        _instrumentPrometheusHelp.TryAdd(instrument, TranslateInstrumentDescriptionToPrometheusHelp(instrument));

        try
        {
            // Always listen to everything - we want to adapt all input metrics to Prometheus metrics.
            listener.EnableMeasurementEvents(instrument);
        }
        catch (Exception ex)
        {
            // Eat exceptions here to ensure no harm comes of failed enabling.
            // The previous generation EventCounter infrastructure has proven quite buggy and while Meters may not be afflicted with the same problems, let's be paranoid.
            Trace.WriteLine($"Failed to enable Meter listening for {instrument.Name}: {ex.Message}");
        }
    }

    private void OnMeasurementRecorded<T>(Instrument instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        where T : struct
    {
        // NOTE: If we throw an exception from this, it can lead to the instrument becoming inoperable (no longer measured). Let's not do that.

        try
        {
            // NB! Order of labels matters in the prometheus-net API. However, in .NET Meters the data is unordered.
            // Therefore, we need to sort the labels to ensure that we always create metrics with the same order.
            var sortedTags = tags.ToArray().OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
            var labelNameCandidates = TagsToLabelNames(sortedTags);
            var labelValueCandidates = TagsToLabelValues(sortedTags);

            // NOTE: As we accept random input from external code here, there is no guarantee that the labels in this code do not conflict with existing static labels.
            // We must therefore take explicit action here to prevent conflict (as prometheus-net will detect and fault on such conflicts). We do this by inspecting
            // the internals of the factory to identify conflicts with any static labels, and remove those lables from the Meters API data point (static overrides dynamic).
            FilterLabelsToAvoidConflicts(labelNameCandidates, labelValueCandidates, _inheritedStaticLabelNames, out var labelNames, out var labelValues);

            var value = Convert.ToDouble(measurement);

            // We do not represent any of the "counter" style .NET meter types as counters because they may be re-created on the .NET Meters side at any time, decrementing the value!

            if (instrument is Counter<T>)
            {
                var handle = _factory.CreateGauge(_instrumentPrometheusNames[instrument], _instrumentPrometheusHelp[instrument], labelNames);

                // A measurement is the increment.
                handle.WithLease(x => x.Inc(value), labelValues);
            }
            else if (instrument is ObservableCounter<T>)
            {
                var handle = _factory.CreateGauge(_instrumentPrometheusNames[instrument], _instrumentPrometheusHelp[instrument], labelNames);

                // A measurement is the current value.
                handle.WithLease(x => x.IncTo(value), labelValues);
            }
#if NET7_0_OR_GREATER
            else if (instrument is UpDownCounter<T>)
            {
                var handle = _factory.CreateGauge(_instrumentPrometheusNames[instrument], _instrumentPrometheusHelp[instrument], labelNames);

                // A measurement is the increment.
                handle.WithLease(x => x.Inc(value));
            }
#endif
            else if (instrument is ObservableGauge<T>
#if NET7_0_OR_GREATER
                or ObservableUpDownCounter<T>
#endif
                )
            {
                var handle = _factory.CreateGauge(_instrumentPrometheusNames[instrument], _instrumentPrometheusHelp[instrument], labelNames);

                // A measurement is the current value.
                handle.WithLease(x => x.Set(value), labelValues);
            }
            else if (instrument is Histogram<T>)
            {
                var handle = _factory.CreateHistogram(_instrumentPrometheusNames[instrument], _instrumentPrometheusHelp[instrument], labelNames, new HistogramConfiguration
                {
                    // We outsource the bucket definition to the callback in options, as it might need to be different for different instruments.
                    Buckets = _options.ResolveHistogramBuckets(instrument)
                });

                // A measurement is the observed value.
                handle.WithLease(x => x.Observe(value), labelValues);
            }
            else
            {
                Trace.WriteLine($"Instrument {instrument.Name} is of an unsupported type: {instrument.GetType().Name}.");
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"{instrument.Name} collection failed: {ex.Message}");
        }
    }

    private static void FilterLabelsToAvoidConflicts(string[] nameCandidates, string[] valueCandidates, string[] namesToSkip, out string[] names, out string[] values)
    {
        var acceptedNames = new List<string>(nameCandidates.Length);
        var acceptedValues = new List<string>(valueCandidates.Length);

        for (int i = 0; i < nameCandidates.Length; i++)
        {
            if (namesToSkip.Contains(nameCandidates[i]))
                continue;

            acceptedNames.Add(nameCandidates[i]);
            acceptedValues.Add(valueCandidates[i]);
        }

        names = acceptedNames.ToArray();
        values = acceptedValues.ToArray();
    }

    private void OnMeasurementsCompleted(Instrument instrument, object? state)
    {
        // Called when no more data is coming for an instrument. We do not do anything with the already published metrics because:
        // 1) We operate on a pull model - just because the instrument goes away does not mean that the latest data from it has been pulled.
        // 2) We already have a perfectly satisfactory expiration based lifetime control model, no need to complicate with a second logic alongside.
        // 3) There is no 1:1 mapping between instrument and metric due to allowing flexible label name combinations, which may cause undesirable complexity.

        // We know we will not need this data anymore, though, so we can throw it out.
        _instrumentPrometheusNames.TryRemove(instrument, out _);
        _instrumentPrometheusHelp.TryRemove(instrument, out _);
    }

    private string[] TagsToLabelNames(List<KeyValuePair<string, object?>> tags)
    {
        var labelNames = new string[tags.Count];

        for (var i = 0; i < tags.Count; i++)
        {
            var prometheusLabelName = _tagPrometheusNames.GetOrAdd(tags[i].Key, TranslateTagNameToPrometheusName);
            labelNames[i] = prometheusLabelName;
        }

        return labelNames;
    }

    private string[] TagsToLabelValues(List<KeyValuePair<string, object?>> tags)
    {
        var labelValues = new string[tags.Count];

        for (var i = 0; i < tags.Count; i++)
        {
            labelValues[i] = tags[i].Value?.ToString() ?? "";
        }

        return labelValues;
    }

    // We use these dictionaries to register Prometheus metrics on-demand for different tag sets.
    private static readonly ConcurrentDictionary<Instrument, string> _instrumentPrometheusNames = new();
    private static readonly ConcurrentDictionary<Instrument, string> _instrumentPrometheusHelp = new();

    // We use this dictionary to translate tag names on-demand.
    // Immortal set, we assume we do not get an infinite mix of tag names.
    private static readonly ConcurrentDictionary<string, string> _tagPrometheusNames = new();

    private static string TranslateInstrumentNameToPrometheusName(Instrument instrument)
    {
        // Example input: meter "Foo.Bar.Baz" with instrument "walla-walla"
        // Example output: foo_bar_baz_walla_walla

        return PrometheusNameHelpers.TranslateNameToPrometheusName($"{instrument.Meter.Name}_{instrument.Name}");
    }

    private static string TranslateTagNameToPrometheusName(string tagName)
    {
        // Example input: hello-there
        // Example output: hello_there

        return PrometheusNameHelpers.TranslateNameToPrometheusName(tagName);
    }

    private static string TranslateInstrumentDescriptionToPrometheusHelp(Instrument instrument)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(instrument.Unit))
            sb.Append($"({instrument.Unit}) ");

        sb.Append(instrument.Description);

        // Append the base type name, so we see what type of metric it is.
        sb.Append($" ({instrument.GetType().Name})");

        return sb.ToString();
    }
}
#endif