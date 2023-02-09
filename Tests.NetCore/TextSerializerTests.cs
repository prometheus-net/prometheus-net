using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Prometheus.Tests;

[TestClass]
public class TextSerializerTests
{
    [ClassInitialize]
    public static void BeforeClass(TestContext testContext)
    {
        ObservedExemplar.NowProvider = () => TestNow;
    }

    [ClassCleanup]
    public static void AfterClass()
    {
        ObservedExemplar.NowProvider = ObservedExemplar.DefaultNowProvider;
    }

    [TestMethod]
    public async Task ValidateTextFmtSummaryExposition_Labels()
    {
        var result = await TestCase.Run(factory =>
        {
            var summary = factory.CreateSummary("boom_bam", "", new SummaryConfiguration
            {
                LabelNames = new[] { "blah" },
                Objectives = new[]
                {
                    new QuantileEpsilonPair(0.5, 0.05),
                }
            });

            summary.WithLabels("foo").Observe(3);
        });

        result.ShouldBe(@"# HELP boom_bam 
# TYPE boom_bam summary
boom_bam_sum{blah=""foo""} 3
boom_bam_count{blah=""foo""} 1
boom_bam{blah=""foo"",quantile=""0.5""} 3
");
    }

    [TestMethod]
    public async Task ValidateTextFmtSummaryExposition_NoLabels()
    {
        var result = await TestCase.Run(factory =>
        {
            var summary = factory.CreateSummary("boom_bam", "something", new SummaryConfiguration
            {
                Objectives = new[]
                {
                    new QuantileEpsilonPair(0.5, 0.05),
                }
            });
            summary.Observe(3);
        });

        result.ShouldBe(@"# HELP boom_bam something
# TYPE boom_bam summary
boom_bam_sum 3
boom_bam_count 1
boom_bam{quantile=""0.5""} 3
");
    }


    [TestMethod]
    public async Task ValidateTextFmtGaugeExposition_Labels()
    {
        var result = await TestCase.Run(factory =>
        {
            var gauge = factory.CreateGauge("boom_bam", "", new GaugeConfiguration
            {
                LabelNames = new[] { "blah" }
            });

            gauge.WithLabels("foo").IncTo(10);
        });

        result.ShouldBe(@"# HELP boom_bam 
# TYPE boom_bam gauge
boom_bam{blah=""foo""} 10
");
    }

    [TestMethod]
    public async Task ValidateTextFmtCounterExposition_Labels()
    {
        var result = await TestCase.Run(factory =>
        {
            var counter = factory.CreateCounter("boom_bam", "", new CounterConfiguration
            {
                LabelNames = new[] { "blah" }
            });

            counter.WithLabels("foo").IncTo(10);
        });

        result.ShouldBe("# HELP boom_bam \n" +
                        "# TYPE boom_bam counter\n" +
                        "boom_bam{blah=\"foo\"} 10\n");
    }

    [TestMethod]
    public async Task ValidateTextFmtCounterExposition_TotalSuffixInName()
    {
        var result = await TestCase.Run(factory =>
        {
            var counter = factory.CreateCounter("boom_bam_total", "", new CounterConfiguration
            {
                LabelNames = new[] { "blah" }
            });

            counter.WithLabels("foo").IncTo(10);
        });

        // This tests that the counter exposition format isn't influenced by openmetrics codepaths when it comes to the
        // _total suffix
        result.ShouldBe("# HELP boom_bam_total \n" +
                        "# TYPE boom_bam_total counter\n" +
                        "boom_bam_total{blah=\"foo\"} 10\n");
    }

    [TestMethod]
    public async Task ValidateTextFmtHistogramExposition_Labels()
    {
        var result = await TestCase.Run(factory =>
        {
            var counter = factory.CreateHistogram("boom_bam", "", new HistogramConfiguration
            {
                LabelNames = new[] { "blah" },
                Buckets = new[] { 1.0 }
            });

            counter.WithLabels("foo").Observe(0.5);
        });

        result.ShouldBe(@"# HELP boom_bam 
# TYPE boom_bam histogram
boom_bam_sum{blah=""foo""} 0.5
boom_bam_count{blah=""foo""} 1
boom_bam_bucket{blah=""foo"",le=""1""} 1
boom_bam_bucket{blah=""foo"",le=""+Inf""} 1
");
    }

    [TestMethod]
    public async Task ValidateTextFmtHistogramExposition_NoLabels()
    {
        var result = await TestCase.Run(factory =>
        {
            var counter = factory.CreateHistogram("boom_bam", "something", new HistogramConfiguration
            {
                Buckets = new[] { 1.0, Math.Pow(10, 45) }
            });

            counter.Observe(0.5);
        });

        result.ShouldBe(@"# HELP boom_bam something
# TYPE boom_bam histogram
boom_bam_sum 0.5
boom_bam_count 1
boom_bam_bucket{le=""1""} 1
boom_bam_bucket{le=""1e+45""} 1
boom_bam_bucket{le=""+Inf""} 1
");
    }

    [TestMethod]
    public async Task ValidateOpenMetricsFmtHistogram_Basic()
    {
        var result = await TestCase.RunOpenMetrics(factory =>
        {
            var counter = factory.CreateHistogram("boom_bam", "something", new HistogramConfiguration
            {
                Buckets = new[] { 1, 2.5 }
            });

            counter.Observe(1.5);
            counter.Observe(1);
        });

        // This asserts that the le label has been modified and that we have a EOF
        result.ShouldBe(@"# HELP boom_bam something
# TYPE boom_bam histogram
boom_bam_sum 2.5
boom_bam_count 2
boom_bam_bucket{le=""1.0""} 1
boom_bam_bucket{le=""2.5""} 2
boom_bam_bucket{le=""+Inf""} 2
# EOF
");
    }

    [TestMethod]
    public async Task ValidateOpenMetricsFmtHistogram_WithExemplar()
    {
        var result = await TestCase.RunOpenMetrics(factory =>
        {
            var counter = factory.CreateHistogram("boom_bam", "something", new HistogramConfiguration
            {
                Buckets = new[] { 1, 2.5, 3, Math.Pow(10, 45) }
            });

            counter.Observe(1, Exemplar.From(Exemplar.Pair("traceID", "1")));
            counter.Observe(1.5, Exemplar.From(Exemplar.Pair("traceID", "2")));
            counter.Observe(4, Exemplar.From(Exemplar.Pair("traceID", "3")));
            counter.Observe(Math.Pow(10, 44), Exemplar.From(Exemplar.Pair("traceID", "4")));
        });

        // This asserts histogram OpenMetrics form with exemplars and also using numbers which are large enough for
        // scientific notation
        result.ShouldBe(@"# HELP boom_bam something
# TYPE boom_bam histogram
boom_bam_sum 1e+44
boom_bam_count 4
boom_bam_bucket{le=""1.0""} 1 # {traceID=""1""} 1.0 1668779954.714
boom_bam_bucket{le=""2.5""} 2 # {traceID=""2""} 1.5 1668779954.714
boom_bam_bucket{le=""3.0""} 2
boom_bam_bucket{le=""1e+45""} 4 # {traceID=""4""} 1e+44 1668779954.714
boom_bam_bucket{le=""+Inf""} 4
# EOF
");
    }

    [TestMethod]
    public async Task ValidateOpenMetricsFmtCounter_MultiItemExemplar()
    {
        var result = await TestCase.RunOpenMetrics(factory =>
        {
            var counter = factory.CreateCounter("boom_bam", "", new CounterConfiguration
            {
                LabelNames = new[] { "blah" }
            });

            counter.WithLabels("foo").Inc(1,
                Exemplar.From(Exemplar.Pair("traceID", "1234"), Exemplar.Pair("yaay", "4321")));
        });
        // This asserts that multi-labeled exemplars work as well not supplying a _total suffix in the counter name.
        result.ShouldBe(@"# HELP boom_bam 
# TYPE boom_bam unknown
boom_bam{blah=""foo""} 1.0 # {traceID=""1234"",yaay=""4321""} 1.0 1668779954.714
# EOF
");
    }

    [TestMethod]
    public async Task ValidateOpenMetricsFmtCounter_TotalInNameSuffix()
    {
        var result = await TestCase.RunOpenMetrics(factory =>
        {
            var counter = factory.CreateCounter("boom_bam_total", "", new CounterConfiguration
            {
                LabelNames = new[] { "blah" }
            });

            counter.WithLabels("foo").Inc(1,
                Exemplar.From(Exemplar.Pair("traceID", "1234"), Exemplar.Pair("yaay", "4321")));
        });
        // This tests the shape of OpenMetrics when _total suffix is supplied
        result.ShouldBe(@"# HELP boom_bam 
# TYPE boom_bam counter
boom_bam_total{blah=""foo""} 1.0 # {traceID=""1234"",yaay=""4321""} 1.0 1668779954.714
# EOF
");
    }

    private const double TestNow = 1668779954.714;

    private class TestCase
    {
        private readonly String raw;
        private readonly List<String> lines;

        private TestCase(List<string> lines, string raw)
        {
            this.lines = lines;
            this.raw = raw;
        }

        public static async Task<TestCase> RunOpenMetrics(Action<MetricFactory> register)
        {
            return await Run(register, ExpositionFormat.OpenMetricsText);
        }

        public static async Task<TestCase> Run(Action<MetricFactory> register, ExpositionFormat format = ExpositionFormat.PrometheusText)
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            register(factory);

            using var stream = new MemoryStream();
            await registry.CollectAndExportAsTextAsync(stream, format);

            var lines = new List<String>();
            stream.Position = 0;
            var raw = new StreamReader(stream).ReadToEnd();
            stream.Position = 0;

            using StreamReader reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                lines.Add(reader.ReadLine());
            }

            return new TestCase(lines, raw);
        }

        public void DumpExposition()
        {
            foreach (var line in lines)
            {
                Console.WriteLine(line);
            }
        }

        public void ShouldBe(string expected)
        {
            expected = expected.Replace("\r", "");
            Assert.AreEqual(expected, raw);
        }
    }
}