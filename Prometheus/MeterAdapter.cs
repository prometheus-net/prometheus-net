#if NET6_0_OR_GREATER
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Prometheus;

/// <summary>
/// Publishes .NET Meters as Prometheus metrics.
/// </summary>
public sealed class MeterAdapter : IDisposable
{
    public static IDisposable StartListening() => StartListening(MeterAdapterOptions.Default);

    public static IDisposable StartListening(MeterAdapterOptions options)
    {
        // If we are re-registering an adapter with the default options, just pretend and move on.
        // The purpose of this code is to avoid double-counting metrics if the adapter is registered twice with the default options.
        // This could happen because in 7.0.0 we added automatic registration of the adapters on startup, but the user might still
        // have a manual registration active from 6.0.0 days. We do this small thing here to make upgrading less hassle.
        if (options == MeterAdapterOptions.Default)
        {
            if (options.Registry.PreventMeterAdapterRegistrationWithDefaultOptions)
                return new NoopDisposable();

            options.Registry.PreventMeterAdapterRegistrationWithDefaultOptions = true;
        }

        return new MeterAdapter(options);
    }

    private MeterAdapter(MeterAdapterOptions options)
    {
        _options = options;

        _registry = options.Registry;

        var baseFactory = options.MetricFactory ?? Metrics.WithCustomRegistry(_options.Registry);
        _factory = (ManagedLifetimeMetricFactory)baseFactory.WithManagedLifetime(expiresAfter: options.MetricsExpireAfter);

        _inheritedStaticLabelNames = ((ManagedLifetimeMetricFactory)_factory).GetAllStaticLabelNames().ToArray();

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
    private readonly IManagedLifetimeMetricFactory _factory;
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
            var labelNames = ShortStringSequence.FromLabelNames(tags);
            var value = Convert.ToDouble(measurement);

            // We do not represent any of the "counter" style .NET meter types as counters because they may be re-created on the .NET Meters side at any time, decrementing the value!

            if (instrument is Counter<T>)
            {
                var (handle, labelOrder) = GetOrCreateGauge(instrument, labelNames);

                // A measurement is the increment.
                handle.WithLease(x => x.Inc(value), TagsToLabelValues(tags, labelOrder));
            }
            else if (instrument is ObservableCounter<T>)
            {
                var (handle, labelOrder) = GetOrCreateGauge(instrument, labelNames);

                // A measurement is the current value. We transform it into a Set() to allow the counter to reset itself (unusual but who are we to say no).
                handle.WithLease(x => x.Set(value), TagsToLabelValues(tags, labelOrder));
            }
#if NET7_0_OR_GREATER
            else if (instrument is UpDownCounter<T>)
            {
                var (handle, labelOrder) = GetOrCreateGauge(instrument, labelNames);

                // A measurement is the increment.
                handle.WithLease(x => x.Inc(value), TagsToLabelValues(tags, labelOrder));
            }
#endif
            else if (instrument is ObservableGauge<T>
#if NET7_0_OR_GREATER
                or ObservableUpDownCounter<T>
#endif
                )
            {
                var (handle, labelOrder) = GetOrCreateGauge(instrument, labelNames);

                // A measurement is the current value.
                handle.WithLease(x => x.Set(value), TagsToLabelValues(tags, labelOrder));
            }
            else if (instrument is Histogram<T>)
            {
                var (handle, labelOrder) = GetOrCreateHistogram(instrument, labelNames);

                // A measurement is the observed value.
                handle.WithLease(x => x.Observe(value), TagsToLabelValues(tags, labelOrder));
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

    private static void FilterLabelsToAvoidConflicts<TLabel>(string[] nameCandidates, TLabel[] valueCandidates, string[] namesToSkip, out string[] names, out TLabel[] values)
    {
        var acceptedNames = new List<string>(nameCandidates.Length);
        var acceptedValues = new List<TLabel>(valueCandidates.Length);

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

        // remove all cached metrics for this instrument, which also frees references to the instrument
        if (_instrumentPrometheusLabelNames.TryRemove(instrument, out var labelCombinations))
        {
            foreach (var labels in labelCombinations)
            {
                _cachedGauges.TryRemove((new (instrument), labels), out _);
                _cachedHistograms.TryRemove((new (instrument), labels), out _);
            }
        }
    }

    private void PreprocessLabels(string[] labelNames, out string[] names, out int[] order)
    {
        var prometheusNames = new string[labelNames.Length];
        var labelOrder = new int[labelNames.Length];
        for (int i = 0; i < labelNames.Length; i++)
        {
            prometheusNames[i] = _tagPrometheusNames.GetOrAdd(labelNames[i], TranslateTagNameToPrometheusName);
            labelOrder[i] = i;
        }

        // NB! Order of labels matters in the prometheus-net API. However, in .NET Meters the data is unordered.
        // Therefore, we need to sort the labels to ensure that we always create metrics with the same order.
        Array.Sort<string, int>(keys: prometheusNames, items: labelOrder, StringComparer.Ordinal);

        // NOTE: As we accept random input from external code here, there is no guarantee that the labels in this code do not conflict with existing static labels.
        // We must therefore take explicit action here to prevent conflict (as prometheus-net will detect and fault on such conflicts). We do this by inspecting
        // the internals of the factory to identify conflicts with any static labels, and remove those lables from the Meters API data point (static overrides dynamic).
        FilterLabelsToAvoidConflicts(prometheusNames, labelOrder, _inheritedStaticLabelNames, out names, out order);
    }


    private string[] TagsToLabelValues(ReadOnlySpan<KeyValuePair<string, object?>> tags, int[] labelOrder)
    {
        var labelValues = new string[labelOrder.Length];

        for (var i = 0; i < labelOrder.Length; i++)
        {
            labelValues[i] = tags[labelOrder[i]].Value?.ToString() ?? "";
        }

        return labelValues;
    }

    // We use these dictionaries to register Prometheus metrics on-demand for different tag sets.
    private readonly static ConcurrentDictionary<Instrument, string> _instrumentPrometheusNames = new();
    private readonly static ConcurrentDictionary<Instrument, string> _instrumentPrometheusHelp = new();

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

    // keep track of which labels were used, so we can clear the metric cache when the instrument is removed
    private readonly ConcurrentDictionary<Instrument, ImmutableStack<ShortStringSequence>> _instrumentPrometheusLabelNames = new();
    private void RegisterLabelNames(Instrument instrument, ShortStringSequence labelNames)
    {
        _instrumentPrometheusLabelNames.AddOrUpdate(instrument, ImmutableStack.Create(labelNames), (_, stack) => stack.Push(labelNames));
    }

    private readonly ConcurrentDictionary<(InsrumentWrapper instrument, ShortStringSequence labelNames), (IManagedLifetimeMetricHandle<IGauge> gauge, int[] labelOrder)> _cachedGauges = new();
    private readonly ConcurrentDictionary<(InsrumentWrapper instrument, ShortStringSequence labelNames), (IManagedLifetimeMetricHandle<IHistogram>, int[] labelOrder)> _cachedHistograms = new();

    private (IManagedLifetimeMetricHandle<IGauge>, int[] labelOrder) GetOrCreateGauge(Instrument instrument, in ShortStringSequence labelNames)
    {
        var key = (new InsrumentWrapper(instrument), labelNames);
        if (_cachedGauges.TryGetValue(key, out var existing)) // avoid closure allocation if we already have the value
        {
            return existing;
        }

        return _cachedGauges.GetOrAdd(key, x => {
            PreprocessLabels(x.labelNames.ToArray(), out var labelNames, out var labelOrder);
            var gauge = _factory.CreateGauge(_instrumentPrometheusNames[x.instrument.Value], _instrumentPrometheusHelp[x.instrument.Value], labelNames);
            RegisterLabelNames(instrument, x.labelNames);
            return (gauge, labelOrder);
        });
    }

    private (IManagedLifetimeMetricHandle<IHistogram>, int[] labelOrder) GetOrCreateHistogram(Instrument instrument, in ShortStringSequence labelNames)
    {
        var key = (new InsrumentWrapper(instrument), labelNames);
        if (_cachedHistograms.TryGetValue(key, out var existing)) // avoid closure allocation if we already have the value
        {
            return existing;
        }

        return _cachedHistograms.GetOrAdd(key, x => {
            PreprocessLabels(x.labelNames.ToArray(), out var labelNames, out var labelOrder);
            var configuration = new HistogramConfiguration
            {
                // We outsource the bucket definition to the callback in options, as it might need to be different for different instruments.
                Buckets = _options.ResolveHistogramBuckets(instrument)
            };
            var histogram = _factory.CreateHistogram(_instrumentPrometheusNames[x.instrument.Value], _instrumentPrometheusHelp[x.instrument.Value], labelNames, configuration);
            RegisterLabelNames(instrument, x.labelNames);
            return (histogram, labelOrder);
        });
    }

    /// <summary>
    /// List of strings implementing equality.
    /// If its length is at most 3, it avoids heap allocations.
    /// Intended to be used for label names.
    /// </summary>
    readonly struct ShortStringSequence : IEquatable<ShortStringSequence>
    {
        public readonly string? Item1;
        public readonly string? Item2;
        public readonly string? Item3;
        public readonly string[] Rest;

        public ShortStringSequence(string? item1, string? item2, string? item3, string[] rest)
        {
            Item1 = item1;
            Item2 = item2;
            Item3 = item3;
            Rest = rest;
        }

        public bool Equals(ShortStringSequence other)
        {
            if (Item1 is null)
                return other.Item1 is null;
            if (!StringComparer.Ordinal.Equals(Item1, other.Item1))
                return false;
            if (Item2 is null)
                return other.Item2 is null;
            if (!StringComparer.Ordinal.Equals(Item2, other.Item2))
                return false;
            if (Item3 is null)
                return other.Item3 is null;
            if (!StringComparer.Ordinal.Equals(Item3, other.Item3))
                return false;

            if (Rest.Length != other.Rest.Length)
            {
                return false;
            }

            for (int i = 0; i < Rest.Length; i++)
            {
                if (!StringComparer.Ordinal.Equals(Rest[i], other.Rest[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is ShortStringSequence other && Equals(other);
        public override int GetHashCode()
        {
            if (Item2 is null)
            {
                if (Item1 is null)
                {
                    return 0;
                }
                else
                {
                    return StringComparer.Ordinal.GetHashCode(Item1);
                }
            }

            var h = new HashCode();
            h.Add(StringComparer.Ordinal.GetHashCode(Item1!));
            h.Add(StringComparer.Ordinal.GetHashCode(Item2));
            if (Item3 is not null)
            {
                h.Add(StringComparer.Ordinal.GetHashCode(Item3));

                for (int i = 0; i < Rest.Length; i++)
                {
                    h.Add(StringComparer.Ordinal.GetHashCode(Rest[i]));
                }
            }
            return h.ToHashCode();
        }

        public string[] ToArray()
        {
            if (Item1 is null)
                return Array.Empty<string>();
            if (Item2 is null)
                return new[] { Item1 };
            if (Item3 is null)
                return new[] { Item1, Item2 };
            var result = new string[Rest.Length + 3];
            result[0] = Item1;
            result[1] = Item2;
            result[2] = Item3;
            for (int i = 0; i < Rest.Length; i++)
            {
                result[i + 3] = Rest[i];
            }
            return result;
        }

        public static ShortStringSequence FromLabelNames(ReadOnlySpan<KeyValuePair<string, object?>> labels)
        {
            string[] rest;
            if (labels.Length > 3)
            {
                rest = new string[labels.Length - 3];
                for (int i = 0; i < rest.Length; i++)
                {
                    rest[i] = labels[i + 3].Key;
                }
            }
            else
            {
                rest = Array.Empty<string>();
            }
            var item1 = labels.Length > 0 ? labels[0].Key : null;
            var item2 = labels.Length > 1 ? labels[1].Key : null;
            var item3 = labels.Length > 2 ? labels[2].Key : null;
            return new ShortStringSequence(item1, item2, item3, rest);
        }
    }
    /// <summary>
    /// A reference comparison wrapper for Instrument
    /// Instruments are often compared by == operator, which is reference comparison.
    /// None of the .NET Instrument override Equals and it doesn't seem possible to
    /// roll your own impementation into the Meter class.
    /// Since it's a struct, .NET is also able to optimize away
    /// EqualityComparer{T}.Default used in the dictionary
    /// </summary>
    readonly struct InsrumentWrapper: IEquatable<InsrumentWrapper>
    {
        public readonly Instrument Value;

        public InsrumentWrapper(Instrument value)
        {
            Value = value;
        }

        public bool Equals(InsrumentWrapper other) => ReferenceEquals(Value, other.Value);
        public override bool Equals(object? obj) => obj is InsrumentWrapper other && Equals(other);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(Value);
    }
}
#endif
