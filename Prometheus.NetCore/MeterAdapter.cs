#if NET6_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus;

/// <summary>
/// Monitors all .NET Meters and exposes them as Prometheus counters and gauges.
/// </summary>
/// <remarks>
/// </remarks>
public sealed class MeterAdapter : IDisposable
{
    public static IDisposable StartListening() => new MeterAdapter(MeterAdapterOptions.Default);

    public static IDisposable StartListening(MeterAdapterOptions options) => new MeterAdapter(options);

    private MeterAdapter(MeterAdapterOptions options)
    {
        _options = options;
        _metricFactory = Metrics.WithCustomRegistry(_options.Registry);

        _counter = _metricFactory.CreateCounter("dotnet_meters_counter", "Incrementing counters from the .NET Meters API.", new CounterConfiguration
        {
            LabelNames = new[] { "meter", "instrument", "unit", "description" }
        });

        _gauge = _metricFactory.CreateGauge("dotnet_meters_gauge", "Raw values from the .NET Meters API.", new GaugeConfiguration
        {
            LabelNames = new[] { "meter", "instrument", "unit", "description" }
        });

        _countersListener.InstrumentPublished += OnCounterInstrumentPublished;
        _countersListener.SetMeasurementEventCallback<byte>(OnCounterMeasurement);
        _countersListener.SetMeasurementEventCallback<short>(OnCounterMeasurement);
        _countersListener.SetMeasurementEventCallback<int>(OnCounterMeasurement);
        _countersListener.SetMeasurementEventCallback<long>(OnCounterMeasurement);
        _countersListener.SetMeasurementEventCallback<float>(OnCounterMeasurement);
        _countersListener.SetMeasurementEventCallback<double>(OnCounterMeasurement);
        _countersListener.SetMeasurementEventCallback<decimal>(OnCounterMeasurement);

        _countersListener.Start();

        _gaugesListener.InstrumentPublished += OnGaugeInstrumentPublished;
        _gaugesListener.SetMeasurementEventCallback<byte>(OnGaugeMeasurement);
        _gaugesListener.SetMeasurementEventCallback<short>(OnGaugeMeasurement);
        _gaugesListener.SetMeasurementEventCallback<int>(OnGaugeMeasurement);
        _gaugesListener.SetMeasurementEventCallback<long>(OnGaugeMeasurement);
        _gaugesListener.SetMeasurementEventCallback<float>(OnGaugeMeasurement);
        _gaugesListener.SetMeasurementEventCallback<double>(OnGaugeMeasurement);
        _gaugesListener.SetMeasurementEventCallback<decimal>(OnGaugeMeasurement);

        _gaugesListener.Start();

        Task.Run(TimerLoopAsync);
    }

    public void Dispose()
    {
        _timer.Dispose();
        _countersListener.Dispose();
        _gaugesListener.Dispose();
    }

    private readonly MeterAdapterOptions _options;
    private readonly IMetricFactory _metricFactory;

    private readonly Counter _counter;
    private readonly Gauge _gauge;

    // We use this to poll observable metrics once per second.
    private readonly PeriodicTimer _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

    private async Task TimerLoopAsync()
    {
        while (await _timer.WaitForNextTickAsync())
        {
            _countersListener.RecordObservableInstruments();
            _gaugesListener.RecordObservableInstruments();
        }
    }

    // We use separate listeners for counter-style meters and gauge-style meters.
    // This way we can easily predetermine the type at instrument creation time and not worry about it later.
    private readonly MeterListener _countersListener = new();
    private readonly MeterListener _gaugesListener = new();

    private void OnCounterInstrumentPublished(Instrument instrument, MeterListener listener)
    {
        if (!HasGenericAncestor(instrument.GetType(), typeof(Counter<>))
            && !HasGenericAncestor(instrument.GetType(), typeof(ObservableCounter<>)))
            return; // Not a type that we support on this listener.

        if (!_options.InstrumentFilterPredicate(instrument))
            return;

        listener.EnableMeasurementEvents(instrument);
    }

    private void OnGaugeInstrumentPublished(Instrument instrument, MeterListener listener)
    {
        // We support histograms, as they seem to produce regular events (although we do not treat them as histograms).
        // They are histograms in name only, it seems, as the collection tool needs to actually do the calculations.
        if (!HasGenericAncestor(instrument.GetType(), typeof(Histogram<>))
            && !HasGenericAncestor(instrument.GetType(), typeof(ObservableGauge<>)))
            return; // Not a type that we support on this listener.

        if (!_options.InstrumentFilterPredicate(instrument))
            return;

        listener.EnableMeasurementEvents(instrument);
    }

    private static bool HasGenericAncestor(Type t, Type openGenericAncestor)
    {
        while (true)
        {
            if (t.IsGenericType)
            {
                // Maybe.
                var gtd = t.GetGenericTypeDefinition();

                if (gtd == openGenericAncestor)
                    return true;
            }

            if (t.BaseType == null)
                return false;

            t = t.BaseType;
        }
    }

    private void OnCounterMeasurement(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        _counter.WithLabels(instrument.Meter.Name, instrument.Name, instrument.Unit ?? "", instrument.Description ?? "").Inc(measurement);
    }

    private void OnCounterMeasurement(Instrument instrument, byte measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        OnCounterMeasurement(instrument, (double)measurement, tags, state);
    }

    private void OnCounterMeasurement(Instrument instrument, short measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        OnCounterMeasurement(instrument, (double)measurement, tags, state);
    }

    private void OnCounterMeasurement(Instrument instrument, int measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        OnCounterMeasurement(instrument, (double)measurement, tags, state);
    }

    private void OnCounterMeasurement(Instrument instrument, long measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        OnCounterMeasurement(instrument, (double)measurement, tags, state);
    }

    private void OnCounterMeasurement(Instrument instrument, float measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        OnCounterMeasurement(instrument, (double)measurement, tags, state);
    }

    private void OnCounterMeasurement(Instrument instrument, decimal measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        OnCounterMeasurement(instrument, unchecked((double)measurement), tags, state);
    }

    private void OnGaugeMeasurement(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        _gauge.WithLabels(instrument.Meter.Name, instrument.Name, instrument.Unit ?? "", instrument.Description ?? "").Set(measurement);
    }

    private void OnGaugeMeasurement(Instrument instrument, byte measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        OnGaugeMeasurement(instrument, (double)measurement, tags, state);
    }

    private void OnGaugeMeasurement(Instrument instrument, short measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        OnGaugeMeasurement(instrument, (double)measurement, tags, state);
    }

    private void OnGaugeMeasurement(Instrument instrument, int measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        OnGaugeMeasurement(instrument, (double)measurement, tags, state);
    }

    private void OnGaugeMeasurement(Instrument instrument, long measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        OnGaugeMeasurement(instrument, (double)measurement, tags, state);
    }

    private void OnGaugeMeasurement(Instrument instrument, float measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        OnGaugeMeasurement(instrument, (double)measurement, tags, state);
    }

    private void OnGaugeMeasurement(Instrument instrument, decimal measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        OnGaugeMeasurement(instrument, unchecked((double)measurement), tags, state);
    }
}

#endif