using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Prometheus.Tests;

[TestClass]
public class TextSerializerTests
{
    [TestMethod]
    public async Task ValidateTextFmtSummaryExposition_HappyPath()
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
        // TODO help has a trailing whitespace before the newline
        result.ShouldBe(@"# HELP boom_bam 
# TYPE boom_bam summary
boom_bam_sum{blah=""foo""} 3
boom_bam_count{blah=""foo""} 1
boom_bam{blah=""foo"",quantile=""0.5""} 3
");
    }

    [TestMethod]
    public async Task ValidateTextFmtSummaryExposition_HappyPath_NoLabels()
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
        // TODO help has a trailing whitespace before the newline
        result.ShouldBe(@"# HELP boom_bam something
# TYPE boom_bam summary
boom_bam_sum{} 3
boom_bam_count{} 1
boom_bam{quantile=""0.5""} 3
");
    }


    [TestMethod]
    public async Task ValidateTextFmtGaugeExposition_HappyPath()
    {
        var result = await TestCase.Run(factory =>
        {
            var gauge = factory.CreateGauge("boom_bam", "", new GaugeConfiguration
            {
                LabelNames = new[] { "blah" }
            });

            gauge.WithLabels("foo").IncTo(10);
        });
        // TODO help has a trailing whitespace before the newline
        result.ShouldBe(@"# HELP boom_bam 
# TYPE boom_bam gauge
boom_bam{blah=""foo""} 10
");
    }

    [TestMethod]
    public async Task ValidateTextFmtCounterExposition_HappyPath()
    {
        var result = await TestCase.Run(factory =>
        {
            var counter = factory.CreateCounter("boom_bam", "", new CounterConfiguration
            {
                LabelNames = new[] { "blah" }
            });

            counter.WithLabels("foo").IncTo(10);
        });

        // TODO help has a trailing whitespace before the newline
        result.ShouldBe("# HELP boom_bam \n" +
                        "# TYPE boom_bam counter\n" +
                        "boom_bam{blah=\"foo\"} 10\n");
    }

    [TestMethod]
    public async Task ValidateTextFmtHistogramExposition_HappyPath()
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

        // TODO help has a trailing whitespace before the newline
        result.ShouldBe(@"# HELP boom_bam 
# TYPE boom_bam histogram
boom_bam_sum{blah=""foo""} 0.5
boom_bam_count{blah=""foo""} 1
boom_bam_bucket{blah=""foo"",le=""1""} 1
boom_bam_bucket{blah=""foo"",le=""+Inf""} 1
");
    }

    [TestMethod]
    public async Task ValidateTextFmtHistogramExposition_HappyPath_NoLabels()
    {
        var result = await TestCase.Run(factory =>
        {
            var counter = factory.CreateHistogram("boom_bam", "something", new HistogramConfiguration
            {
                Buckets = new[] { 1.0, 2 }
            });

            counter.Observe(0.5);
        });

        result.ShouldBe(@"# HELP boom_bam something
# TYPE boom_bam histogram
boom_bam_sum{} 0.5
boom_bam_count{} 1
boom_bam_bucket{le=""1""} 1
boom_bam_bucket{le=""2""} 1
boom_bam_bucket{le=""+Inf""} 1
");
    }


    private class TestCase
    {
        private readonly String raw;
        private readonly List<String> lines;

        private TestCase(List<string> lines, string raw)
        {
            this.lines = lines;
            this.raw = raw;
        }


        public static async Task<TestCase> Run(Action<MetricFactory> register)
        {
            var registry = Metrics.NewCustomRegistry();
            var factory = Metrics.WithCustomRegistry(registry);

            register(factory);

            using var stream = new MemoryStream();
            await registry.CollectAndExportAsTextAsync(stream);

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