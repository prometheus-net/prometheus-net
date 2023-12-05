#if NET6_0_OR_GREATER
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.InteropServices;
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
        _createGaugeFunc = CreateGauge;
        _createHistogramFunc = CreateHistogram;

        _options = options;

        _registry = options.Registry;

        var baseFactory = options.MetricFactory ?? Metrics.WithCustomRegistry(_options.Registry);
        _factory = (ManagedLifetimeMetricFactory)baseFactory.WithManagedLifetime(expiresAfter: options.MetricsExpireAfter);

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
            lock (_disposedLock)
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

    private bool _disposed;
    private readonly object _disposedLock = new();

    public void Dispose()
    {
        lock (_disposedLock)
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

    private void OnMeasurementRecorded<TMeasurement>(
        Instrument instrument,
        TMeasurement measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
        where TMeasurement : struct
    {
        // NOTE: If we throw an exception from this, it can lead to the instrument becoming inoperable (no longer measured). Let's not do that.

        // We assemble and sort the label values in a temporary buffer. If the metric instance is already known
        // to prometheus-net, this means no further memory allocation for the label values is required below.
        var labelValuesBuffer = ArrayPool<string>.Shared.Rent(tags.Length);

        try
        {
            double value = unchecked(measurement switch
            {
                byte x => x,
                short x => x,
                int x => x,
                long x => x,
                float x => (double)x,
                double x => x,
                decimal x => (double)x,
                _ => throw new NotSupportedException($"Measurement type {typeof(TMeasurement).Name} is not supported.")
            });

            // We do not represent any of the "counter" style .NET meter types as counters because
            // they may be re-created on the .NET Meters side at any time, decrementing the value!

            if (instrument is Counter<TMeasurement>)
            {
                var context = GetOrCreateGaugeContext(instrument, tags);
                var labelValues = CopyTagValuesToLabelValues(context.PrometheusLabelValueIndexes, tags, labelValuesBuffer.AsSpan());

                // A measurement is the increment.
                context.MetricInstanceHandle.WithLease(_incrementGaugeFunc, value, labelValues);
            }
            else if (instrument is ObservableCounter<TMeasurement>)
            {
                var context = GetOrCreateGaugeContext(instrument, tags);
                var labelValues = CopyTagValuesToLabelValues(context.PrometheusLabelValueIndexes, tags, labelValuesBuffer.AsSpan());

                // A measurement is the current value. We transform it into a Set() to allow the counter to reset itself (unusual but who are we to say no).
                context.MetricInstanceHandle.WithLease(_setGaugeFunc, value, labelValues);
            }
#if NET7_0_OR_GREATER
            else if (instrument is UpDownCounter<TMeasurement>)
            {
                var context = GetOrCreateGaugeContext(instrument, tags);
                var labelValues = CopyTagValuesToLabelValues(context.PrometheusLabelValueIndexes, tags, labelValuesBuffer.AsSpan());

                // A measurement is the increment.
                context.MetricInstanceHandle.WithLease(_incrementGaugeFunc, value, labelValues);
            }
#endif
            else if (instrument is ObservableGauge<TMeasurement>
#if NET7_0_OR_GREATER
                or ObservableUpDownCounter<TMeasurement>
#endif
                )
            {
                var context = GetOrCreateGaugeContext(instrument, tags);
                var labelValues = CopyTagValuesToLabelValues(context.PrometheusLabelValueIndexes, tags, labelValuesBuffer.AsSpan());

                // A measurement is the current value.
                context.MetricInstanceHandle.WithLease(_setGaugeFunc, value, labelValues);
            }
            else if (instrument is Histogram<TMeasurement>)
            {
                var context = GetOrCreateHistogramContext(instrument, tags);
                var labelValues = CopyTagValuesToLabelValues(context.PrometheusLabelValueIndexes, tags, labelValuesBuffer.AsSpan());

                // A measurement is the observed value.
                context.MetricInstanceHandle.WithLease(_observeHistogramFunc, value, labelValues);
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
        finally
        {
            ArrayPool<string>.Shared.Return(labelValuesBuffer);
        }
    }

    private static void IncrementGauge(double value, IGauge gauge) => gauge.Inc(value);
    private static readonly Action<double, IGauge> _incrementGaugeFunc = IncrementGauge;

    private static void SetGauge(double value, IGauge gauge) => gauge.Set(value);
    private static readonly Action<double, IGauge> _setGaugeFunc = SetGauge;

    private static void ObserveHistogram(double value, IHistogram histogram) => histogram.Observe(value);
    private static readonly Action<double, IHistogram> _observeHistogramFunc = ObserveHistogram;

    // Cache key: Instrument + user-ordered list of label names.
    //   NB! The same Instrument may be cached multiple times, with the same label names in a different order!
    private readonly struct CacheKey(Instrument instrument, StringSequence meterLabelNames) : IEquatable<CacheKey>
    {
        public Instrument Instrument { get; } = instrument;

        // Order is whatever was provided by the caller of the .NET Meters API.
        public StringSequence MeterLabelNames { get; } = meterLabelNames;

        public override readonly bool Equals(object? obj) => obj is CacheKey other && Equals(other);

        public override readonly int GetHashCode() => _hashCode;
        private readonly int _hashCode = HashCode.Combine(instrument, meterLabelNames);

        public readonly bool Equals(CacheKey other) => Instrument == other.Instrument && MeterLabelNames.Equals(other.MeterLabelNames);
    }

    // Cache value: Prometheus metric handle + Prometheus-ordered indexes into original Meters tags list.
    //   Not all Meter tags may be preserved, as some may have conflicted with static labels and been filtered out.
    private sealed class MetricContext<TMetricInterface>(
        IManagedLifetimeMetricHandle<TMetricInterface> metricInstanceHandle,
        int[] prometheusLabelValueIndexes)
        where TMetricInterface : ICollectorChild
    {
        public IManagedLifetimeMetricHandle<TMetricInterface> MetricInstanceHandle { get; } = metricInstanceHandle;

        // Index into the .NET Meters API labels list, indicating which original label to take the value from.
        public int[] PrometheusLabelValueIndexes { get; } = prometheusLabelValueIndexes;
    }

    private readonly Dictionary<CacheKey, MetricContext<IGauge>> _gaugeCache = new();
    private readonly ReaderWriterLockSlim _gaugeCacheLock = new();

    private readonly Dictionary<CacheKey, MetricContext<IHistogram>> _histogramCache = new();
    private readonly ReaderWriterLockSlim _histogramCacheLock = new();

    private MetricContext<IGauge> GetOrCreateGaugeContext(Instrument instrument, in ReadOnlySpan<KeyValuePair<string, object?>> tags)
        => GetOrCreateMetricContext(instrument, tags, _createGaugeFunc, _gaugeCacheLock, _gaugeCache);

    private MetricContext<IHistogram> GetOrCreateHistogramContext(Instrument instrument, in ReadOnlySpan<KeyValuePair<string, object?>> tags)
        => GetOrCreateMetricContext(instrument, tags, _createHistogramFunc, _histogramCacheLock, _histogramCache);

    private IManagedLifetimeMetricHandle<IGauge> CreateGauge(Instrument instrument, string name, string help, string[] labelNames)
        => _factory.CreateGauge(name, help, labelNames, null);
    private readonly Func<Instrument, string, string, string[], IManagedLifetimeMetricHandle<IGauge>> _createGaugeFunc;

    private IManagedLifetimeMetricHandle<IHistogram> CreateHistogram(Instrument instrument, string name, string help, string[] labelNames)
        => _factory.CreateHistogram(name, help, labelNames, new HistogramConfiguration
        {
            // We outsource the bucket definition to the callback in options, as it might need to be different for different instruments.
            Buckets = _options.ResolveHistogramBuckets(instrument)
        });
    private readonly Func<Instrument, string, string, string[], IManagedLifetimeMetricHandle<IHistogram>> _createHistogramFunc;

    private MetricContext<TMetricInstance> GetOrCreateMetricContext<TMetricInstance>(
        Instrument instrument,
        in ReadOnlySpan<KeyValuePair<string, object?>> tags,
        Func<Instrument, string, string, string[], IManagedLifetimeMetricHandle<TMetricInstance>> metricFactory,
        ReaderWriterLockSlim cacheLock,
        Dictionary<CacheKey, MetricContext<TMetricInstance>> cache)
        where TMetricInstance : ICollectorChild
    {
        // Use a pooled array for the cache key if we are performing a lookup.
        // This avoids allocating a new array if the context is already cached.
        var meterLabelNamesBuffer = ArrayPool<string>.Shared.Rent(tags.Length);
        var meterLabelNamesCount = tags.Length;

        try
        {
            for (var i = 0; i < tags.Length; i++)
                meterLabelNamesBuffer[i] = tags[i].Key;

            var meterLabelNames = StringSequence.From(meterLabelNamesBuffer.AsMemory(0, meterLabelNamesCount));
            var cacheKey = new CacheKey(instrument, meterLabelNames);

            cacheLock.EnterReadLock();

            try
            {
                // In the common case, we will find the context in the cache and can return it here without any memory allocation.
                if (cache.TryGetValue(cacheKey, out var context))
                    return context;
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }
        finally
        {
            ArrayPool<string>.Shared.Return(meterLabelNamesBuffer);
        }

        // If we got here, we did not find the context in the cache. Make a new one.
        return CreateMetricContext(instrument, tags, metricFactory, cacheLock, cache);
    }

    private MetricContext<TMetricInstance> CreateMetricContext<TMetricInstance>(
        Instrument instrument,
        in ReadOnlySpan<KeyValuePair<string, object?>> tags,
        Func<Instrument, string, string, string[], IManagedLifetimeMetricHandle<TMetricInstance>> metricFactory,
        ReaderWriterLockSlim cacheLock,
        Dictionary<CacheKey, MetricContext<TMetricInstance>> cache)
        where TMetricInstance : ICollectorChild
    {
        var meterLabelNamesBuffer = new string[tags.Length];

        for (var i = 0; i < tags.Length; i++)
            meterLabelNamesBuffer[i] = tags[i].Key;

        var meterLabelNames = StringSequence.From(meterLabelNamesBuffer);
        var cacheKey = new CacheKey(instrument, meterLabelNames);

        // Create the context before taking any locks, to avoid holding the cache too long.
        DeterminePrometheusLabels(tags, out var prometheusLabelNames, out var prometheusLabelValueIndexes);
        var metricHandle = metricFactory(instrument, _instrumentPrometheusNames[instrument], _instrumentPrometheusHelp[instrument], prometheusLabelNames);
        var newContext = new MetricContext<TMetricInstance>(metricHandle, prometheusLabelValueIndexes);

        cacheLock.EnterWriteLock();

        try
        {
#if NET
            // It could be that someone beats us to it! Probably not, though.
            if (cache.TryAdd(cacheKey, newContext))
                return newContext;

            return cache[cacheKey];
#else
            // On .NET Fx we need to do the pessimistic case first because there is no TryAdd().
            if (cache.TryGetValue(cacheKey, out var context))
                return context;

            cache.Add(cacheKey, newContext);
            return newContext;
#endif
        }
        finally
        {
            cacheLock.ExitWriteLock();
        }
    }

    private void DeterminePrometheusLabels(
        in ReadOnlySpan<KeyValuePair<string, object?>> tags,
        out string[] prometheusLabelNames,
        out int[] prometheusLabelValueIndexes)
    {
        var originalsCount = tags.Length;

        // Prometheus name of the label.
        var namesBuffer = ArrayPool<string>.Shared.Rent(originalsCount);
        // Index into the original label list.
        var indexesBuffer = ArrayPool<int>.Shared.Rent(originalsCount);
        // Whether the label should be skipped entirely (because it conflicts with a static label).
        var skipFlagsBuffer = ArrayPool<bool>.Shared.Rent(originalsCount);

        try
        {
            for (var i = 0; i < tags.Length; i++)
            {
                var prometheusName = _tagPrometheusNames.GetOrAdd(tags[i].Key, _translateTagNameToPrometheusNameFunc);

                namesBuffer[i] = prometheusName;
                indexesBuffer[i] = i;
            }

            // The order of labels matters in the prometheus-net API. However, in .NET Meters the tags are unordered.
            // Therefore, we need to sort the labels to ensure that we always create metrics with the same order.
            Array.Sort(keys: namesBuffer, items: indexesBuffer, index: 0, length: originalsCount, StringComparer.Ordinal);

            // NOTE: As we accept random input from external code here, there is no guarantee that the labels in this code
            // do not conflict with existing static labels. We must therefore take explicit action here to prevent conflict
            // (as prometheus-net will detect and fault on such conflicts). We do this by inspecting the internals of the
            // factory to identify conflicts with any static labels, and remove those lables from the Meters API data point
            // (static overrides dynamic) if there is a match (by just skipping them in our output index set).
            var preservedLabelCount = 0;

            for (var i = 0; i < tags.Length; i++)
            {
                skipFlagsBuffer[i] = _inheritedStaticLabelNames.Contains(namesBuffer[i], StringComparer.Ordinal);

                if (skipFlagsBuffer[i] == false)
                    preservedLabelCount++;
            }

            prometheusLabelNames = new string[preservedLabelCount];
            prometheusLabelValueIndexes = new int[preservedLabelCount];

            var nextIndex = 0;

            for (var i = 0; i < tags.Length; i++)
            {
                if (skipFlagsBuffer[i])
                    continue;

                prometheusLabelNames[nextIndex] = namesBuffer[i];
                prometheusLabelValueIndexes[nextIndex] = indexesBuffer[i];
                nextIndex++;
            }
        }
        finally
        {
            ArrayPool<bool>.Shared.Return(skipFlagsBuffer);
            ArrayPool<int>.Shared.Return(indexesBuffer);
            ArrayPool<string>.Shared.Return(namesBuffer);
        }
    }

    private void OnMeasurementsCompleted(Instrument instrument, object? state)
    {
        // Called when no more data is coming for an instrument. We do not do anything with the already published metrics because:
        // 1) We operate on a pull model - just because the instrument goes away does not mean that the latest data from it has been pulled.
        // 2) We already have a perfectly satisfactory expiration based lifetime control model, no need to complicate with a second logic alongside.
        // 3) There is no 1:1 mapping between instrument and metric due to allowing flexible label name combinations, which may cause undesirable complexity.

        // We also cannot clear our mapping collections yet because it is possible that some measurement observations are still in progress!
        // In other words, this may be called before the last OnMeasurementRecorded() call for the instrument has completed (perhaps even started?).
        // The entire adapter data set will be collected when the Prometheus registry itself is garbage collected.
    }

    private static ReadOnlySpan<string> CopyTagValuesToLabelValues(
        int[] prometheusLabelValueIndexes,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        Span<string> labelValues)
    {
        for (var i = 0; i < prometheusLabelValueIndexes.Length; i++)
        {
            var index = prometheusLabelValueIndexes[i];
            labelValues[i] = tags[index].Value?.ToString() ?? "";
        }

        return labelValues[..prometheusLabelValueIndexes.Length];
    }

    // We use these dictionaries to register Prometheus metrics on-demand for different instruments.
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

    private static readonly Func<string, string> _translateTagNameToPrometheusNameFunc = TranslateTagNameToPrometheusName;

    [ThreadStatic]
    private static StringBuilder? _prometheusHelpBuilder;

    // If the string builder grows over this, we throw it away and use a new one next time to avoid keeping a large buffer around.
    private const int PrometheusHelpBuilderReusableCapacity = 1 * 1024;

    private static string TranslateInstrumentDescriptionToPrometheusHelp(Instrument instrument)
    {
        _prometheusHelpBuilder ??= new(PrometheusHelpBuilderReusableCapacity);

        if (!string.IsNullOrWhiteSpace(instrument.Unit))
            _prometheusHelpBuilder.Append($"({instrument.Unit}) ");

        _prometheusHelpBuilder.Append(instrument.Description);

        // Append the base type name, so we see what type of metric it is.
        _prometheusHelpBuilder.Append($" ({instrument.GetType().Name})");

        var result = _prometheusHelpBuilder.ToString();

        // If it grew too big, throw it away.
        if (_prometheusHelpBuilder.Capacity > PrometheusHelpBuilderReusableCapacity)
            _prometheusHelpBuilder = null;
        else
            _prometheusHelpBuilder.Clear();

        return result;
    }
}
#endif
