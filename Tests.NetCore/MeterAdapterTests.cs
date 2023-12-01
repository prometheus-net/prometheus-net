using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SDM = System.Diagnostics.Metrics;

namespace Prometheus.Tests;

[TestClass]
public sealed class MeterAdapterTests : IDisposable
{
    private readonly CollectorRegistry _registry;
    private readonly MetricFactory _metrics;
    private readonly SDM.Meter _meter = new("test");
    private readonly SDM.Counter<long> _intCounter;
    private readonly SDM.Counter<double> _floatCounter;
    private readonly IDisposable _adapter;

    public MeterAdapterTests()
    {
        _registry = Metrics.NewCustomRegistry();
        _metrics = Metrics.WithCustomRegistry(_registry);

        _intCounter = _meter.CreateCounter<long>("int_counter");
        _floatCounter = _meter.CreateCounter<double>("float_counter");

        _registry = Metrics.NewCustomRegistry();
        _metrics = Metrics.WithCustomRegistry(_registry);

        _adapter = MeterAdapter.StartListening(new MeterAdapterOptions
        {
            InstrumentFilterPredicate = instrument =>
            {
                return instrument.Meter == _meter;
            },
            Registry = _registry,
            MetricFactory = _metrics,
            ResolveHistogramBuckets = instrument => new double[] { 1, 2, 3, 4 },
        });
    }

    private static FakeSerializer SerializeMetrics(CollectorRegistry registry)
    {
        var serializer = new FakeSerializer();
        registry.CollectAndSerializeAsync(serializer, default).Wait();
        return serializer;
    }

    private double GetValue(string meterName, params (string name, string value)[] labels) =>
        GetValue(_registry, meterName, labels);
    private double GetValue(CollectorRegistry registry, string meterName, params (string name, string value)[] labels)
    {
        var serializer = SerializeMetrics(registry);
        if (serializer.Data.Count == 0)
            throw new Exception("No metrics found");
        var labelsString = string.Join(",", labels.Select(l => $"{l.name}=\"{l.value}\""));
        foreach (var d in serializer.Data)
        {
            Console.WriteLine($"{d.name} {d.labels} {d.canonicalLabel} {d.value}");

            if (d.name == meterName && d.labels == labelsString)
            {
                return d.value;
            }
        }
        if (serializer.Data.Any(d => d.name == meterName))
            throw new Exception($"Metric {meterName}{{{labelsString}}} not found, only these labels were found: {string.Join(" / ", serializer.Data.Where(d => d.name == meterName).Select(d => d.labels))}");

        throw new Exception($"Metric {meterName} not found, only these metrics were found: {string.Join(" / ", serializer.Data.Select(d => d.name).Distinct())}");
    }

    [TestMethod]
    public void CounterInt()
    {
        _intCounter.Add(1);
        Assert.AreEqual(1, GetValue("test_int_counter"));
        _intCounter.Add(2);
        Assert.AreEqual(3, GetValue("test_int_counter"));
    }

    [TestMethod]
    public void CounterFloat()
    {
        _floatCounter.Add(1);
        Assert.AreEqual(1, GetValue("test_float_counter"));
        _floatCounter.Add(0.002);
        Assert.AreEqual(1.002, GetValue("test_float_counter"));
    }

    [TestMethod]
    public void CounterLabels()
    {
        _intCounter.Add(1, new("l1", "value"), new("l2", 111));
        Assert.AreEqual(1, GetValue("test_int_counter", ("l1", "value"), ("l2", "111")));
        _intCounter.Add(1000);
        _intCounter.Add(1000, new("l1", "value"), new("l2", 0));
        _intCounter.Add(1000, new KeyValuePair<string, object?>("l1", "value"));
        _intCounter.Add(1, new("l2", 111), new("l1", "value"));
        Assert.AreEqual(2, GetValue("test_int_counter", ("l1", "value"), ("l2", "111")));
        Assert.AreEqual(1000, GetValue("test_int_counter", ("l1", "value"), ("l2", "0")));
        Assert.AreEqual(1000, GetValue("test_int_counter", ("l1", "value")));
        Assert.AreEqual(1000, GetValue("test_int_counter"));
    }

    [TestMethod]
    public void LabelRenaming()
    {
        _intCounter.Add(1, new("my-label", 1), new("Another.Label", 1));
        Assert.AreEqual(1, GetValue("test_int_counter", ("another_label", "1"), ("my_label", "1")));
    }


    [TestMethod]
    public void MultipleInstances()
    {
        _intCounter.Add(1000);

        var registry2 = Metrics.NewCustomRegistry();
        var metrics2 = Metrics.WithCustomRegistry(registry2);

        var adapter2 = MeterAdapter.StartListening(new MeterAdapterOptions
        {
            InstrumentFilterPredicate = instrument =>
            {
                return instrument.Meter == _meter;
            },
            Registry = registry2,
            MetricFactory = metrics2,
            ResolveHistogramBuckets = instrument => new double[] { 1, 2, 3, 4 },
        });

        _intCounter.Add(1);
        Assert.AreEqual(1001, GetValue("test_int_counter"));
        Assert.AreEqual(1, GetValue(registry2, "test_int_counter"));

        adapter2.Dispose();

        _intCounter.Add(1);
        Assert.AreEqual(1002, GetValue("test_int_counter"));
        Assert.AreEqual(1, GetValue(registry2, "test_int_counter"));
    }

    public void Dispose()
    {
        _adapter.Dispose();
    }

    class FakeSerializer : IMetricsSerializer
    {
        public List<(string name, string labels, string canonicalLabel, double value, ObservedExemplar exemplar)> Data = new();
        public Task FlushAsync(CancellationToken cancel) => Task.CompletedTask;
        public ValueTask WriteEnd(CancellationToken cancel) => default;

        public ValueTask WriteFamilyDeclarationAsync(string name, byte[] nameBytes, byte[] helpBytes, MetricType type, byte[] typeBytes, CancellationToken cancel) => default;

        public ValueTask WriteMetricPointAsync(byte[] name, byte[] flattenedLabels, CanonicalLabel canonicalLabel, double value, ObservedExemplar exemplar, byte[] suffix, CancellationToken cancel)
        {
            Data.Add((
                name: Encoding.UTF8.GetString(name),
                labels: Encoding.UTF8.GetString(flattenedLabels),
                canonicalLabel: canonicalLabel.ToString(),
                value: value,
                exemplar: exemplar
            ));
            return default;
        }

        public ValueTask WriteMetricPointAsync(byte[] name, byte[] flattenedLabels, CanonicalLabel canonicalLabel, long value, ObservedExemplar exemplar, byte[] suffix, CancellationToken cancel) =>
            WriteMetricPointAsync(name, flattenedLabels, canonicalLabel, (double)value, exemplar, suffix, cancel);
    }
}
