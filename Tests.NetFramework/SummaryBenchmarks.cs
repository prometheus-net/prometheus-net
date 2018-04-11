using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus.Advanced.DataContracts;
using Prometheus.Internal;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Prometheus.Tests
{
    [TestClass]
    public class SummaryBenchmarks
    {
        [DataTestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(4)]
        [DataRow(8)]
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

            Trace.WriteLine($"{w} tasks doing  {N} observations took {stopwatch.Elapsed.TotalMilliseconds} milliseconds");
        }

        [DataTestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(4)]
        [DataRow(8)]
        public void BenchmarkSummaryWrite(int w)
        {
            var stopwatch = new Stopwatch();

            var summary = new Summary("test_summary", "helpless", new string[0]);
            var child = new Summary.Child();
            var now = new DateTime(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            child.Init(summary, LabelValues.Empty, now, true);

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

            Trace.WriteLine($"{w} tasks doing {N} writes took {stopwatch.Elapsed.TotalMilliseconds} milliseconds");
        }
    }
}
