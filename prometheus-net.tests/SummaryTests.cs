using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Prometheus.Advanced.DataContracts;
using Prometheus.Internal;
using Prometheus.SummaryImpl;

namespace Prometheus.Tests
{
    
    [TestFixture]
    public class SummaryTests
    {
        [Test]
        public void TestSummaryConcurrency([Range(0, 1000000, 10000)] int n)
        {
            var random = new Random(42);
            var mutations = n%10000 + 10000;
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

            var m = sum.Collect().metric.Single().summary;

            Assert.That(m.sample_count, Is.EqualTo(mutations * concLevel));

            var got = m.sample_sum;
            var want = sampleSum;
            Assert.That(Math.Abs(got-want)/want, Is.LessThanOrEqualTo(0.001));

            var objectives = Summary.DefObjectives.Select(_ => _.Quantile).ToArray();
            Array.Sort(objectives);

            for (var i = 0; i < objectives.Length; i++)
            {
                var wantQ = Summary.DefObjectives.ElementAt(i);
                var epsilon = wantQ.Epsilon;
                var gotQ = m.quantile[i].quantile;
                var gotV = m.quantile[i].value;
                var minMax = GetBounds(allVars, wantQ.Quantile, epsilon);

                Assert.That(gotQ, Is.Not.NaN);
                Assert.That(gotV, Is.Not.NaN);
                Assert.That(minMax.Item1, Is.Not.NaN);
                Assert.That(minMax.Item2, Is.Not.NaN);

                Assert.That(gotQ, Is.EqualTo(wantQ.Quantile));
                Assert.That(gotV, Is.GreaterThanOrEqualTo(minMax.Item1));
                Assert.That(gotV, Is.LessThanOrEqualTo(minMax.Item2));
            }
        }

        [Test]
        public void TestSummaryDecay()
        {
            var baseTime = new DateTime(2015, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            
            var sum = new Summary("test_summary", "helpless", new string[0], objectives: new List<QuantileEpsilonPair> {new QuantileEpsilonPair(0.1d, 0.001d)}, maxAge: TimeSpan.FromSeconds(100), ageBuckets: 10);
            var child = new Summary.Child();
            child.Init(sum, LabelValues.Empty, baseTime);
            
            Advanced.DataContracts.Summary m;
            var metric = new Metric();
            
            for (var i = 0; i < 1000; i++)
            {
                var now = baseTime.AddSeconds(i);
                child.Observe(i, now);
                
                if (i%10 == 0)
                {
                    child.Populate(metric, now);
                    m = metric.summary;
                    var got = m.quantile[0].value;
                    var want = Math.Max((double) i/10, (double) i - 90);

                    Assert.That(Math.Abs(got-want), Is.LessThanOrEqualTo(1), $"{i}. got {got} want {want}");
                }
            }

            // Wait for MaxAge without observations and make sure quantiles are NaN.
            child.Populate(metric, baseTime.AddSeconds(1000).AddSeconds(100));
            m = metric.summary;

            Assert.That(m.quantile[0].value, Is.NaN);
        }

        [Test]
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
            var metric = new Metric();
            summary.Populate(metric, DateTime.UtcNow);
            var m = metric.summary;

            Assert.That(m.sample_count, Is.EqualTo(numObservations * numIterations));
            Assert.That(m.sample_sum, Is.EqualTo(expectedSum));
            Assert.That(m.quantile.Single(_ => _.quantile.Equals(0.5)).value, Is.EqualTo(50).Within(2));
            Assert.That(m.quantile.Single(_ => _.quantile.Equals(0.9)).value, Is.EqualTo(90).Within(2));
            Assert.That(m.quantile.Single(_ => _.quantile.Equals(0.99)).value, Is.EqualTo(99).Within(2));
        }

        static Tuple<double, double> GetBounds(double[] vars, double q, double epsilon)
        {
            // TODO: This currently tolerates an error of up to 2*ε. The error must
            // be at most ε, but for some reason, it's sometimes slightly
            // higher. That's a bug.
            var n = (double)vars.Length;
            var lower = (int) ((q - 2*epsilon)*n);
            var upper = (int) Math.Ceiling((q + 2 * epsilon) * n);

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
