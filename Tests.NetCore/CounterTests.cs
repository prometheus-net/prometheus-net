using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Prometheus.Tests;

[TestClass]
public class CounterTests
{
    private CollectorRegistry _registry;
    private MetricFactory _metrics;

    public CounterTests()
    {
        _registry = Metrics.NewCustomRegistry();
        _metrics = Metrics.WithCustomRegistry(_registry);
    }

    [TestMethod]
    public void IncTo_IncrementsButDoesNotDecrement()
    {
        var counter = _metrics.CreateCounter("xxx", "xxx");

        counter.IncTo(100);
        Assert.AreEqual(100, counter.Value);

        counter.IncTo(100);
        Assert.AreEqual(100, counter.Value);

        counter.IncTo(10);
        Assert.AreEqual(100, counter.Value);
    }

    [TestMethod]
    public async Task ObserveExemplar_WithDefaultExemplarProvider_UsesDefaultOnlyWhenNoExplicitExemplarProvided()
    {
        const string defaultExemplarData = "this_is_the_default_exemplar";
        const string explicitExemplarData = "this_is_the_explicit_exemplar";

        var counter = _metrics.CreateCounter("xxx", "", new CounterConfiguration
        {
            ExemplarBehavior = new ExemplarBehavior
            {
                DefaultExemplarProvider = (_, _) => Exemplar.From(Exemplar.Pair(defaultExemplarData, defaultExemplarData))
            }
        });

        // No exemplar provided, expect to see default.
        counter.Inc();

        var serialized = await _registry.CollectAndSerializeToStringAsync(ExpositionFormat.OpenMetricsText);
        StringAssert.Contains(serialized, defaultExemplarData);

        counter.Inc(Exemplar.From(Exemplar.Pair(explicitExemplarData, explicitExemplarData)));

        serialized = await _registry.CollectAndSerializeToStringAsync(ExpositionFormat.OpenMetricsText);
        StringAssert.Contains(serialized, explicitExemplarData);
    }

    [TestMethod]
    public async Task ObserveExemplar_WithLimitedRecordingInterval_RecordsOnlyAfterIntervalElapses()
    {
        const string firstData = "this_is_the_first_exemplar";
        const string secondData = "this_is_the_second_exemplar";
        const string thirdData = "this_is_the_third_exemplar";

        var interval = TimeSpan.FromMinutes(5);

        var counter = _metrics.CreateCounter("xxx", "", new CounterConfiguration
        {
            ExemplarBehavior = new ExemplarBehavior
            {
                NewExemplarMinInterval = interval
            }
        });

        double timestampSeconds = 0;
        ChildBase.ExemplarRecordingTimestampProvider = () => timestampSeconds;
        
        try
        {
            counter.Inc(Exemplar.From(Exemplar.Pair(firstData, firstData)));

            var serialized = await _registry.CollectAndSerializeToStringAsync(ExpositionFormat.OpenMetricsText);
            StringAssert.Contains(serialized, firstData);

            // Attempt to record a new exemplar immediately - should fail because interval has not elapsed.
            counter.Inc(Exemplar.From(Exemplar.Pair(secondData, secondData)));

            serialized = await _registry.CollectAndSerializeToStringAsync(ExpositionFormat.OpenMetricsText);
            StringAssert.Contains(serialized, firstData);

            // Wait for enough time to elapse - now it should work.
            timestampSeconds = interval.TotalSeconds;

            counter.Inc(Exemplar.From(Exemplar.Pair(thirdData, thirdData)));

            serialized = await _registry.CollectAndSerializeToStringAsync(ExpositionFormat.OpenMetricsText);
            StringAssert.Contains(serialized, thirdData);
        }
        finally
        {
            ChildBase.ExemplarRecordingTimestampProvider = ChildBase.DefaultExemplarRecordingTimestampProvider;
        }
    }

    [TestMethod]
    public void ObserveExemplar_ReusingInstance_Throws()
    {
        var counter = _metrics.CreateCounter("xxx", "");

        var exemplar = Exemplar.From(Exemplar.Pair("foo", "bar"));

        counter.Inc(exemplar);

        Assert.ThrowsException<InvalidOperationException>(() => counter.Inc(exemplar));
    }
}
