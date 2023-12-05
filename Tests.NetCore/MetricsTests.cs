using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Prometheus.Tests;

[TestClass]
public sealed class MetricsTests
{
    private CollectorRegistry _registry;
    private MetricFactory _metrics;

    public MetricsTests()
    {
        _registry = Metrics.NewCustomRegistry();
        _metrics = Metrics.WithCustomRegistry(_registry);
    }

    [TestMethod]
    public void api_usage()
    {
        var gauge = _metrics.CreateGauge("name1", "help1");
        gauge.Inc();
        Assert.AreEqual(1, gauge.Value);
        gauge.Inc(3.2);
        Assert.AreEqual(4.2, gauge.Value);
        gauge.Set(4);
        Assert.AreEqual(4, gauge.Value);
        gauge.Dec(0.2);
        Assert.AreEqual(3.8, gauge.Value);

        Assert.ThrowsException<ArgumentException>(() => gauge.Labels("1"));

        var counter = _metrics.CreateCounter("name2", "help2", "label1");
        counter.Inc();
        counter.Inc(3.2);
        counter.Inc(0);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => counter.Inc(-1));
        Assert.AreEqual(4.2, counter.Value);

        Assert.AreEqual(0, counter.Labels("a").Value);
        counter.Labels("a").Inc(3.3);
        counter.Labels("a").Inc(1.1);
        Assert.AreEqual(4.4, counter.Labels("a").Value);
    }

    [TestMethod]
    public async Task CreateCounter_WithDifferentRegistry_CreatesIndependentCounters()
    {
        var registry1 = Metrics.NewCustomRegistry();
        var registry2 = Metrics.NewCustomRegistry();
        var counter1 = Metrics.WithCustomRegistry(registry1)
            .CreateCounter("counter", "");
        var counter2 = Metrics.WithCustomRegistry(registry2)
            .CreateCounter("counter", "");

        Assert.AreNotSame(counter1, counter2);

        counter1.Inc();
        counter2.Inc();

        Assert.AreEqual(1, counter1.Value);
        Assert.AreEqual(1, counter2.Value);

        var serializer1 = Substitute.For<IMetricsSerializer>();
        await registry1.CollectAndSerializeAsync(serializer1, default);

        var serializer2 = Substitute.For<IMetricsSerializer>();
        await registry2.CollectAndSerializeAsync(serializer2, default);

        await serializer1.ReceivedWithAnyArgs().WriteFamilyDeclarationAsync(default, default, default, default, default, default);
        await serializer1.ReceivedWithAnyArgs().WriteMetricPointAsync(default, default, default, default(double), default, default, default);

        await serializer2.ReceivedWithAnyArgs().WriteFamilyDeclarationAsync(default, default, default, default, default, default);
        await serializer2.ReceivedWithAnyArgs().WriteMetricPointAsync(default, default, default, default(double), default, default, default);
    }

    [TestMethod]
    public async Task Export_FamilyWithOnlyNonpublishedUnlabeledMetrics_ExportsFamilyDeclaration()
    {
        // See https://github.com/prometheus-net/prometheus-net/issues/196
        var metric = _metrics.CreateCounter("my_family", "", new CounterConfiguration
        {
            SuppressInitialValue = true
        });

        var serializer = Substitute.For<IMetricsSerializer>();
        await _registry.CollectAndSerializeAsync(serializer, default);

        await serializer.ReceivedWithAnyArgs(1).WriteFamilyDeclarationAsync(default, default, default, default, default, default);
        await serializer.DidNotReceiveWithAnyArgs().WriteMetricPointAsync(default, default, default, default(double), default, default, default);
        await serializer.DidNotReceiveWithAnyArgs().WriteMetricPointAsync(default, default, default, default, default, default, default);
        serializer.ClearReceivedCalls();

        metric.Inc();
        metric.Unpublish();

        await _registry.CollectAndSerializeAsync(serializer, default);

        await serializer.ReceivedWithAnyArgs(1).WriteFamilyDeclarationAsync(default, default, default, default, default, default);
        await serializer.DidNotReceiveWithAnyArgs().WriteMetricPointAsync(default, default, default, default(double), default, default, default);
        await serializer.DidNotReceiveWithAnyArgs().WriteMetricPointAsync(default, default, default, default, default, default, default);
    }

    [TestMethod]
    public async Task Export_FamilyWithOnlyNonpublishedLabeledMetrics_ExportsFamilyDeclaration()
    {
        // See https://github.com/prometheus-net/prometheus-net/issues/196
        var metric = _metrics.CreateCounter("my_family", "", new[] { "labelname" }, new CounterConfiguration
        {
            SuppressInitialValue = true,
        });

        var instance = metric.WithLabels("labelvalue");

        var serializer = Substitute.For<IMetricsSerializer>();
        await _registry.CollectAndSerializeAsync(serializer, default);

        await serializer.ReceivedWithAnyArgs(1).WriteFamilyDeclarationAsync(default, default, default, default, default, default);
        await serializer.DidNotReceiveWithAnyArgs().WriteMetricPointAsync(default, default, default, default(double), default, default, default);
        await serializer.DidNotReceiveWithAnyArgs().WriteMetricPointAsync(default, default, default, default, default, default, default);
        serializer.ClearReceivedCalls();

        instance.Inc();
        instance.Unpublish();

        await _registry.CollectAndSerializeAsync(serializer, default);

        await serializer.ReceivedWithAnyArgs(1).WriteFamilyDeclarationAsync(default, default, default, default, default, default);
        await serializer.DidNotReceiveWithAnyArgs().WriteMetricPointAsync(default, default, default, default(double), default, default, default);
        await serializer.DidNotReceiveWithAnyArgs().WriteMetricPointAsync(default, default, default, default, default, default, default);
    }

    [TestMethod]
    public async Task DisposeChild_RemovesMetric()
    {
        var metric = _metrics.CreateCounter("my_family", "", new[] { "labelname" }, new CounterConfiguration
        {
            SuppressInitialValue = true,
        });

        var instance = metric.WithLabels("labelvalue");
        instance.Inc();

        var serializer = Substitute.For<IMetricsSerializer>();
        await _registry.CollectAndSerializeAsync(serializer, default);

        await serializer.ReceivedWithAnyArgs(1).WriteFamilyDeclarationAsync(default, default, default, default, default, default);
        await serializer.ReceivedWithAnyArgs(1).WriteMetricPointAsync(default, default, default, default(double), default, default, default);
        serializer.ClearReceivedCalls();

        instance.Dispose();

        await _registry.CollectAndSerializeAsync(serializer, default);

        await serializer.ReceivedWithAnyArgs(1).WriteFamilyDeclarationAsync(default, default, default, default, default, default);
        await serializer.DidNotReceiveWithAnyArgs().WriteMetricPointAsync(default, default, default, default(double), default, default, default);
        await serializer.DidNotReceiveWithAnyArgs().WriteMetricPointAsync(default, default, default, default, default, default, default);
    }

    [TestMethod]
    public void histogram_no_buckets()
    {
        try
        {
            _metrics.CreateHistogram("hist", "help", new HistogramConfiguration
            {
                Buckets = new double[0]
            });

            Assert.Fail("Expected an exception");
        }
        catch (ArgumentException ex)
        {
            Assert.AreEqual("Histogram must have at least one bucket", ex.Message);
        }
    }

    [TestMethod]
    public void histogram_buckets_do_not_increase()
    {
        try
        {
            _metrics.CreateHistogram("hist", "help", new HistogramConfiguration
            {
                Buckets = new double[] { 0.5, 0.1 }
            });

            Assert.Fail("Expected an exception");
        }
        catch (ArgumentException ex)
        {
            Assert.AreEqual("Bucket values must be increasing", ex.Message);
        }
    }

    [TestMethod]
    public void histogram_exponential_buckets_are_correct()
    {
        var bucketsStart = 1.1;
        var bucketsFactor = 2.4;
        var bucketsCount = 4;

        var buckets = Histogram.ExponentialBuckets(bucketsStart, bucketsFactor, bucketsCount);

        Assert.AreEqual(bucketsCount, buckets.Length);
        Assert.AreEqual(1.1, buckets[0]);
        Assert.AreEqual(2.64, buckets[1]);
        Assert.AreEqual(6.336, buckets[2]);
        Assert.AreEqual(15.2064, buckets[3]);
    }

    [TestMethod]
    public void histogram_exponential_buckets_with_non_positive_count_throws()
    {
        var bucketsStart = 1;
        var bucketsFactor = 2;

        Assert.ThrowsException<ArgumentException>(() => Histogram.ExponentialBuckets(bucketsStart, bucketsFactor, -1));
        Assert.ThrowsException<ArgumentException>(() => Histogram.ExponentialBuckets(bucketsStart, bucketsFactor, 0));
    }

    [TestMethod]
    public void histogram_exponential_buckets_with_non_positive_start_throws()
    {
        var bucketsFactor = 2;
        var bucketsCount = 5;

        Assert.ThrowsException<ArgumentException>(() => Histogram.ExponentialBuckets(-1, bucketsFactor, bucketsCount));
        Assert.ThrowsException<ArgumentException>(() => Histogram.ExponentialBuckets(0, bucketsFactor, bucketsCount));
    }

    [TestMethod]
    public void histogram_exponential_buckets_with__factor_less_than_one_throws()
    {
        var bucketsStart = 1;
        var bucketsCount = 5;

        Assert.ThrowsException<ArgumentException>(() => Histogram.ExponentialBuckets(bucketsStart, 0.9, bucketsCount));
        Assert.ThrowsException<ArgumentException>(() => Histogram.ExponentialBuckets(bucketsStart, 0, bucketsCount));
        Assert.ThrowsException<ArgumentException>(() => Histogram.ExponentialBuckets(bucketsStart, -1, bucketsCount));
    }

    [TestMethod]
    public void histogram_linear_buckets_are_correct()
    {
        var bucketsStart = 1.1;
        var bucketsWidth = 2.4;
        var bucketsCount = 4;

        var buckets = Histogram.LinearBuckets(bucketsStart, bucketsWidth, bucketsCount);

        Assert.AreEqual(bucketsCount, buckets.Length);
        Assert.AreEqual(1.1, buckets[0]);
        Assert.AreEqual(3.5, buckets[1]);
        Assert.AreEqual(5.9, buckets[2]);
        Assert.AreEqual(8.3, buckets[3]);
    }

    [TestMethod]
    public void histogram_linear_buckets_with_non_positive_count_throws()
    {
        var bucketsStart = 1;
        var bucketsWidth = 2;

        Assert.ThrowsException<ArgumentException>(() => Histogram.LinearBuckets(bucketsStart, bucketsWidth, -1));
        Assert.ThrowsException<ArgumentException>(() => Histogram.LinearBuckets(bucketsStart, bucketsWidth, 0));
    }

    [TestMethod]
    public void same_labels_return_same_instance()
    {
        var gauge = _metrics.CreateGauge("name1", "help1", "label1");

        var labelled1 = gauge.Labels("1");

        var labelled2 = gauge.Labels("1");

        Assert.AreSame(labelled2, labelled1);
    }

    [TestMethod]
    public async Task CreateMetric_WithSameMetadataButDifferentLabels_CreatesMetric()
    {
        // This is a deviation from standard Prometheus practices, where you can only use a metric name with a single set of label names.
        // Instead, this library allows the same metric name to be used with different sets of label names, as long as all other metadata exists.
        // The reason for this is that we want to support using prometheus-net as a bridge to report data originating from metrics systems
        // that do not have such limitations (such as the .NET 6 Meters API). Such scenarios can create any combinations of label names.
        // This is permissible by OpenMetrics, though violates the Prometheus client authoring requirements (which is OK - what can you do).

        var gauge1 = _metrics.CreateGauge("name1", "h");
        var gauge2 = _metrics.CreateGauge("name1", "h", "label1");
        var gauge3 = _metrics.CreateGauge("name1", "h", "label2");
        var gauge4 = _metrics.CreateGauge("name1", "h", "label1", "label3");

        // We expect all the metrics registered to be unique instances.
        Assert.AreNotSame(gauge1, gauge2);
        Assert.AreNotSame(gauge2, gauge3);
        Assert.AreNotSame(gauge3, gauge4);

        var gauge1Again = _metrics.CreateGauge("name1", "h");
        var gauge2Again = _metrics.CreateGauge("name1", "h", "label1");
        var gauge3Again = _metrics.CreateGauge("name1", "h", "label2");
        var gauge4Again = _metrics.CreateGauge("name1", "h", "label1", "label3");

        // We expect the instances to be sticky to the specific set of label names.
        Assert.AreSame(gauge1, gauge1Again);
        Assert.AreSame(gauge2, gauge2Again);
        Assert.AreSame(gauge3, gauge3Again);
        Assert.AreSame(gauge4, gauge4Again);

        var canary1 = 543289;
        var canary2 = 735467;
        var canary3 = 627864;
        var canary4 = 837855;

        gauge1.Set(canary1);
        gauge2.Set(canary2);
        gauge3.Set(canary3);
        gauge4.Set(canary4);

        var serialized = await _registry.CollectAndSerializeToStringAsync();

        // We expect all of them to work (to publish data) and to work independently.
        StringAssert.Contains(serialized, canary1.ToString());
        StringAssert.Contains(serialized, canary2.ToString());
        StringAssert.Contains(serialized, canary3.ToString());
        StringAssert.Contains(serialized, canary4.ToString());

        // We expect them all to be serialized as different metric instances in the same metric family.
        var familyDeclaration = "# TYPE name1 gauge";
        StringAssert.Contains(serialized, familyDeclaration);
        var firstIndex = serialized.IndexOf(familyDeclaration);
        var lastIndex = serialized.LastIndexOf(familyDeclaration);

        Assert.AreEqual(firstIndex, lastIndex);
    }

    [TestMethod]
    public void cannot_create_metrics_with_the_same_name_and_labels_but_different_type()
    {
        _metrics.CreateGauge("name1", "h", "label1");
        try
        {
            _metrics.CreateCounter("name1", "h", "label1");
            Assert.Fail("should have thrown");
        }
        catch (InvalidOperationException e)
        {
            Assert.AreEqual("Collector of a different type with the same name is already registered.", e.Message);
        }
    }

    [TestMethod]
    public void metric_names()
    {
        Assert.ThrowsException<ArgumentException>(() => _metrics.CreateGauge("my-metric", "help"));
        Assert.ThrowsException<ArgumentException>(() => _metrics.CreateGauge("my!metric", "help"));
        Assert.ThrowsException<ArgumentException>(() => _metrics.CreateGauge("%", "help"));
        Assert.ThrowsException<ArgumentException>(() => _metrics.CreateGauge("5a", "help"));
        Assert.ThrowsException<ArgumentException>(() => _metrics.CreateGauge("a:3", "help"));

        _metrics.CreateGauge("abc", "help");
        _metrics.CreateGauge("myMetric2", "help");
    }

    [TestMethod]
    public void label_names()
    {
        Assert.ThrowsException<ArgumentException>(() => _metrics.CreateGauge("a", "help1", "my-label"));
        Assert.ThrowsException<ArgumentException>(() => _metrics.CreateGauge("b", "help1", "my!label"));
        Assert.ThrowsException<ArgumentException>(() => _metrics.CreateGauge("c", "help1", "my%label"));
        Assert.ThrowsException<ArgumentException>(() => _metrics.CreateHistogram("d", "help1", "le"));
        Assert.ThrowsException<ArgumentException>(() => _metrics.CreateHistogram("e", "help1", "my:label"));
        _metrics.CreateGauge("f", "help1", "good_name");

        Assert.ThrowsException<ArgumentException>(() => _metrics.CreateGauge("g", "help1", "__reserved"));
    }

    [TestMethod]
    public void label_values()
    {
        var metric = _metrics.CreateGauge("a", "help1", "mylabelname");

        metric.Labels("");
        metric.Labels("mylabelvalue");
        Assert.ThrowsException<NotSupportedException>(() => metric.Labels(new string[] { null }));
    }

    [TestMethod]
    public void GetAllLabelValues_GetsThemAll()
    {
        var metric = _metrics.CreateGauge("ahdgfln", "ahegrtijpm", "a", "b", "c");
        metric.Labels("1", "2", "3");
        metric.Labels("4", "5", "6");

        var values = metric.GetAllLabelValues().OrderBy(v => v[0]).ToArray();

        Assert.AreEqual(2, values.Length);

        Assert.AreEqual(3, values[0].Length);
        Assert.AreEqual("1", values[0][0]);
        Assert.AreEqual("2", values[0][1]);
        Assert.AreEqual("3", values[0][2]);

        Assert.AreEqual(3, values[1].Length);
        Assert.AreEqual("4", values[1][0]);
        Assert.AreEqual("5", values[1][1]);
        Assert.AreEqual("6", values[1][2]);
    }

    [TestMethod]
    public void GetAllLabelValues_DoesNotGetUnlabelled()
    {
        var metric = _metrics.CreateGauge("ahdggfagfln", "ahegrgftijpm");
        metric.Inc();

        var values = metric.GetAllLabelValues().ToArray();

        Assert.AreEqual(0, values.Length);
    }
}
