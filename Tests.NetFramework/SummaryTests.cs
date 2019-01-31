using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus.SummaryImpl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Prometheus.Tests
{
    [TestClass]
    public class SummaryTests
    {
        [DataTestMethod]
        [DataRow(0 * 100000)]
        [DataRow(1 * 100000)]
        [DataRow(2 * 100000)]
        [DataRow(3 * 100000)]
        [DataRow(4 * 100000)]
        [DataRow(5 * 100000)]
        [DataRow(6 * 100000)]
        [DataRow(7 * 100000)]
        [DataRow(8 * 100000)]
        [DataRow(9 * 100000)]
        [DataRow(10 * 100000)]
        public void TestSummaryConcurrency(int n)
        {
            var random = new Random(42);
            var mutations = n % 10000 + 10000L;
            var concLevel = (n / 10000) % 5 + 1;
            var total = mutations * concLevel;

            var sum = new Summary("test_summary", "helpless", new string[0]);
            var allVars = new double[total];
            double sampleSum = 0;
            var tasks = new List<Task>();

            for (var i = 0; i < concLevel; i++)
            {
                var vals = new double[mutations];
                for (var j = 0; j < mutations; j++)
                {
                    var v = random.NormDouble();
                    vals[j] = v;
                    allVars[i * mutations + j] = v;
                    sampleSum += v;
                }

                tasks.Add(Task.Factory.StartNew(() =>
                {
                    foreach (var v in vals)
                        sum.Observe(v);
                }));
            }

            Task.WaitAll(tasks.ToArray());

            Array.Sort(allVars);

            var m = sum.Collect().Metrics.Single().Summary;

            Assert.AreEqual(mutations * concLevel, m.SampleCount);

            var got = m.SampleSum;
            var want = sampleSum;
            Assert.IsTrue(Math.Abs(got - want) / want <= 0.001);

            var objectives = Summary.DefObjectives.Select(_ => _.Quantile).ToArray();
            Array.Sort(objectives);

            for (var i = 0; i < objectives.Length; i++)
            {
                var wantQ = Summary.DefObjectives.ElementAt(i);
                var epsilon = wantQ.Epsilon;
                var gotQ = m.Quantiles[i].Quantile;
                var gotV = m.Quantiles[i].Value;
                var minMax = GetBounds(allVars, wantQ.Quantile, epsilon);

                Assert.IsFalse(double.IsNaN(gotQ));
                Assert.IsFalse(double.IsNaN(gotV));
                Assert.IsFalse(double.IsNaN(minMax.Item1));
                Assert.IsFalse(double.IsNaN(minMax.Item2));

                Assert.AreEqual(wantQ.Quantile, gotQ);
                Assert.IsTrue(gotV >= minMax.Item1);
                Assert.IsTrue(gotV <= minMax.Item2);
            }
        }

        [TestMethod]
        public void TestSummaryDecay()
        {
            var baseTime = new DateTime(2015, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var sum = new Summary("test_summary", "helpless", new string[0], objectives: new List<QuantileEpsilonPair> { new QuantileEpsilonPair(0.1d, 0.001d) }, maxAge: TimeSpan.FromSeconds(100), ageBuckets: 10);
            var child = new Summary.Child();
            child.Init(sum, LabelValues.Empty, baseTime, true);

            SummaryData m;
            var metric = new MetricData();

            for (var i = 0; i < 1000; i++)
            {
                var now = baseTime.AddSeconds(i);
                child.Observe(i, now);

                if (i % 10 == 0)
                {
                    child.Populate(metric, now);
                    m = metric.Summary;
                    var got = m.Quantiles[0].Value;
                    var want = Math.Max((double)i / 10, (double)i - 90);

                    Assert.IsTrue(Math.Abs(got - want) <= 1, $"{i}. got {got} want {want}");
                }
            }

            // Wait for MaxAge without observations and make sure quantiles are NaN.
            child.Populate(metric, baseTime.AddSeconds(1000).AddSeconds(100));
            m = metric.Summary;

            Assert.IsTrue(double.IsNaN(m.Quantiles[0].Value));
        }

        [TestMethod]
        public void TestSummary()
        {
            var summary = Metrics.CreateSummary("Summary", "helpless", "labelName").Labels("labelValue");

            // Default objectives are 0.5, 0.9, 0.99 quantile
            const int numIterations = 1000;
            const int numObservations = 100;

            var expectedSum = 0;
            for (var iteration = 0; iteration < numIterations; iteration++)
            {
                // 100 observations from 0 to 99
                for (var observation = 0; observation < numObservations; observation++)
                {
                    summary.Observe(observation);
                    expectedSum += observation;
                }
            }
            var metric = new MetricData();
            summary.Populate(metric, DateTime.UtcNow);
            var m = metric.Summary;

            Assert.AreEqual(numObservations * numIterations, m.SampleCount);
            Assert.AreEqual(expectedSum, m.SampleSum);

            var q05 = m.Quantiles.Single(_ => _.Quantile.Equals(0.5)).Value;
            var q09 = m.Quantiles.Single(_ => _.Quantile.Equals(0.9)).Value;
            var q099 = m.Quantiles.Single(_ => _.Quantile.Equals(0.99)).Value;
            Assert.IsTrue(Math.Abs(50 - q05) <= 2);
            Assert.IsTrue(Math.Abs(90 - q09) <= 2);
            Assert.IsTrue(Math.Abs(99 - q099) <= 2);
        }

        static Tuple<double, double> GetBounds(double[] vars, double q, double epsilon)
        {
            // TODO: This currently tolerates an error of up to 2*ε. The error must
            // be at most ε, but for some reason, it's sometimes slightly
            // higher. That's a bug.
            var n = (double)vars.Length;
            var lower = (int)((q - 2 * epsilon) * n);
            var upper = (int)Math.Ceiling((q + 2 * epsilon) * n);

            var min = vars[0];
            if (lower > 1)
            {
                min = vars[lower - 1];
            }

            var max = vars[vars.Length - 1];
            if (upper < vars.Length)
            {
                max = vars[upper - 1];
            }

            return new Tuple<double, double>(min, max);
        }
    }
}
