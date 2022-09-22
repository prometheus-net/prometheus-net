using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.RegularExpressions;

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
        _factory = Metrics.WithCustomRegistry(_options.Registry)
            .WithManagedLifetime(expiresAfter: options.MetricsExpireAfter);

        _listener.InstrumentPublished = OnInstrumentPublished;
        _listener.MeasurementsCompleted += OnMeasurementsCompleted;
        _listener.SetMeasurementEventCallback<byte>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<short>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<int>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<long>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<float>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<double>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<decimal>(OnMeasurementRecorded);

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
    private readonly IManagedLifetimeMetricFactory _factory;

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

        _instrumentPrometheusNames.TryAdd(instrument, TranslateInstrumentNameToPrometheusName(instrument));
        _instrumentPrometheusHelp.TryAdd(instrument, TranslateInstrumentDescriptionToPrometheusHelp(instrument));

        // Always listen to everything - we want to adapt all input metrics to Prometheus metrics.
        listener.EnableMeasurementEvents(instrument);
    }

    private void OnMeasurementRecorded<T>(Instrument instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        where T : struct
    {
        // NOTE: If we throw an exception from this, it can lead to the instrument becoming inoperable (no longer measured). Let's not do that.

        // TODO: We can do some good here by reducing the constant memory allocation that is happening (options, leases, etc).

        try
        {
            var labelNames = TagsToLabelNames(tags);
            var labelValues = TagsToLabelValues(tags);

            var value = Convert.ToDouble(measurement);

            if (instrument is Counter<T>)
            {
                var counterHandle = _factory.CreateCounter(_instrumentPrometheusNames[instrument], _instrumentPrometheusHelp[instrument], new CounterConfiguration
                {
                    LabelNames = labelNames
                });

                using (counterHandle.AcquireLease(out var counter, labelValues))
                {
                    // A measurement is the increment.
                    counter.Inc(value);
                }
            }
            else if (instrument is ObservableCounter<T>)
            {
                var counterHandle = _factory.CreateCounter(_instrumentPrometheusNames[instrument], _instrumentPrometheusHelp[instrument], new CounterConfiguration
                {
                    LabelNames = labelNames
                });

                using (counterHandle.AcquireLease(out var counter, labelValues))
                {
                    // A measurement is the current value.
                    counter.IncTo(value);
                }
            }
            /* .NET 7: else if (instrument is UpDownCounter<T>)
            {
                var gaugeHandle = _factory.CreateGauge(_instrumentPrometheusNames[instrument], _instrumentPrometheusHelp[instrument], new GaugeConfiguration
                {
                    LabelNames = labelNames
                });

                using (gaugeHandle.AcquireLease(out var gauge, labelValues))
                {
                    // A measurement is the increment.
                    gauge.Inc(value);
                }
            }*/
            else if (instrument is ObservableGauge<T> /* .NET 7: or ObservableUpDownCounter<T>*/)
            {
                var gaugeHandle = _factory.CreateGauge(_instrumentPrometheusNames[instrument], _instrumentPrometheusHelp[instrument], new GaugeConfiguration
                {
                    LabelNames = labelNames
                });

                using (gaugeHandle.AcquireLease(out var gauge, labelValues))
                {
                    // A measurement is the current value.
                    gauge.Set(value);
                }
            }
            else if (instrument is Histogram<T>)
            {
                var histogramHandle = _factory.CreateHistogram(_instrumentPrometheusNames[instrument], _instrumentPrometheusHelp[instrument], new HistogramConfiguration
                {
                    LabelNames = labelNames,
                    // We oursource the bucket definition to the callback in options, as it might need to be different for different instruments.
                    Buckets = _options.ResolveHistogramBuckets(instrument)
                });

                using (histogramHandle.AcquireLease(out var metric, labelValues))
                {
                    // A measurement is the observed value.
                    metric.Observe(value);
                }
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

    private string[] TagsToLabelNames(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var labelNames = new string[tags.Length];

        for (var i = 0; i < tags.Length; i++)
        {
            var prometheusLabelName = _tagPrometheusNames.GetOrAdd(tags[i].Key, TranslateTagNameToPrometheusName);
            labelNames[i] = prometheusLabelName;
        }

        return labelNames;
    }

    private string[] TagsToLabelValues(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var labelValues = new string[tags.Length];

        for (var i = 0; i < tags.Length; i++)
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

    private static readonly Regex NameRegex = new Regex("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);
    private const string FirstCharacterCharset = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_";
    private const string NonFirstCharacterCharset = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_0123456789";

    private static string TranslateInstrumentNameToPrometheusName(Instrument instrument)
    {
        // Example input: meter "Foo.Bar.Baz" with instrument "walla-walla"
        // Example output: foo_bar_baz_walla_walla

        return TranslateNameToPrometheusName($"{instrument.Meter.Name}_{instrument.Name}");
    }

    private static string TranslateTagNameToPrometheusName(string tagName)
    {
        // Example input: hello-there
        // Example output: hello_there

        return TranslateNameToPrometheusName(tagName);
    }

    private static string TranslateNameToPrometheusName(string inputName)
    {
        // Transformations done:
        // * all lowercase
        // * special characters to underscore
        // * must match: [a-zA-Z_][a-zA-Z0-9_]*
        //   * colon is "permitted" by spec but reserved for recording rules

        var sb = new StringBuilder();

        foreach (char inputCharacter in inputName)
        {
            // All lowercase.
            var c = Char.ToLowerInvariant(inputCharacter);

            if (sb.Length == 0)
            {
                // If first character is not from allowed charset, prefix it with underscore to minimize first character data loss.
                if (!FirstCharacterCharset.Contains(c))
                    sb.Append('_');

                sb.Append(c);
            }
            else
            {
                // Standard rules.
                // If character is not permitted, replace with underscore. Simple as that!
                if (!NonFirstCharacterCharset.Contains(c))
                    sb.Append('_');
                else
                    sb.Append(c);
            }
        }

        var name = sb.ToString();

        // Sanity check.
        if (!NameRegex.IsMatch(name))
            throw new Exception("Self-check failed: generated name did not match our own naming rules.");

        return name;
    }

    private static string TranslateInstrumentDescriptionToPrometheusHelp(Instrument instrument)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(instrument.Unit))
            sb.AppendFormat($"({instrument.Unit}) ");

        sb.Append(instrument.Description);

        return sb.ToString();
    }
}