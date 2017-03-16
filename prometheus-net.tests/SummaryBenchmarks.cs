using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Prometheus.Advanced.DataContracts;
using Prometheus.Internal;
using System.Diagnostics;

namespace Prometheus.Tests
{
    [TestFixture]
    public class SummaryBenchmarks
    {
        class SummaryBenchmarkData
        {
            public static IEnumerable<TestCaseData> ObserveTestCases
            {
                get
                {
                    yield return new TestCaseData(1);
                    yield return new TestCaseData(2);
                    yield return new TestCaseData(4);
                    yield return new TestCaseData(8);
                }
            }

            public static IEnumerable<TestCaseData> WriteTestCases
            {
                get
                {
                    yield return new TestCaseData(1);
                    yield return new TestCaseData(2);
                    yield return new TestCaseData(4);
                    yield return new TestCaseData(8);
                }
            }
        }

        [Test, TestCaseSource(typeof(SummaryBenchmarkData), nameof(SummaryBenchmarkData.ObserveTestCases))]
        public void BenchmarkSummaryObserve(int w)
        {
            var stopwatch = new Stopwatch();
            
            const int N = 100000;
            var summary = new Summary("test_summary", "helpless", new string[0]);
            var tasks = new Task[w];

            stopwatch.Start();
            for (var i = 0; i < w; i++)
            {
                tasks[i] = Task.Factory.StartNew(() =>
                {
                    for (var j = 0; j < N; j++)
                        summary.Observe(j);
                });
            }

            Task.WaitAll(tasks);
            stopwatch.Stop();

            TestContext.WriteLine($"{w} tasks doing  {N} observations took {stopwatch.Elapsed.TotalMilliseconds} milliseconds");
        }

        [Test, TestCaseSource(typeof(SummaryBenchmarkData), nameof(SummaryBenchmarkData.WriteTestCases))]
        public void BencharmSummaryWrite(int w)
        {
            var stopwatch = new Stopwatch();

            var summary = new Summary("test_summary", "helpless", new string[0]);
            var child = new Summary.Child();
            var now = new DateTime(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            child.Init(summary, LabelValues.Empty, now);

            const int N = 1000;

            for (var obsNum = 0; obsNum < 1000000; obsNum++)
            {
                child.Observe(obsNum, now);
            }

            stopwatch.Start();
            var tasks = new Task[w];
            for (var taskNum = 0; taskNum < w; taskNum++)
            {
                var metric = new Metric();

                tasks[taskNum] = Task.Factory.StartNew(() =>
                {
                    for (var i = 0; i < N; i++)
                        child.Populate(metric, now);
                });
            }
            
            Task.WaitAll(tasks);
            stopwatch.Stop();

            TestContext.WriteLine($"{w} tasks doing {N} writes took {stopwatch.Elapsed.TotalMilliseconds} milliseconds");
        }
    }
}
