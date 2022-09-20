using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus.SummaryImpl;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Prometheus.Tests
{
    [TestClass]
    public class QuantileStreamTests
    {
        readonly IReadOnlyList<QuantileEpsilonPair> _targets = new List<QuantileEpsilonPair>
        {
            new QuantileEpsilonPair(0.01, 0.001),
            new QuantileEpsilonPair(0.10, 0.01),
            new QuantileEpsilonPair(0.50, 0.05),
            new QuantileEpsilonPair(0.90, 0.01),
            new QuantileEpsilonPair(0.99, 0.001)
        };

        readonly double[] _lowQuantiles = { 0.01, 0.1, 0.5 };
        readonly double[] _highQuantiles = { 0.99, 0.9, 0.5 };

        const double RelativeEpsilon = 0.01;

        [TestMethod]
        public void TestTargetedQuery()
        {
            var random = new Random(42);
            var s = QuantileStream.NewTargeted(_targets);
            var a = PopulateStream(s, random);
            VerifyPercsWithAbsoluteEpsilon(a, s);
        }

        [TestMethod]
        public void TestLowBiasedQuery()
        {
            var random = new Random(42);
            var s = QuantileStream.NewLowBiased(RelativeEpsilon);
            var a = PopulateStream(s, random);
            VerifyLowPercsWithRelativeEpsilon(a, s);
        }

        [TestMethod]
        public void TestHighBiasedQuery()
        {
            var random = new Random(42);
            var s = QuantileStream.NewHighBiased(RelativeEpsilon);
            var a = PopulateStream(s, random);
            VerifyHighPercsWithRelativeEpsilon(a, s);
        }

        [TestMethod]
        public void TestUncompressed()
        {
            var q = QuantileStream.NewTargeted(_targets);

            for (var i = 100; i > 0; i--)
            {
                q.Insert(i);
            }

            Assert.AreEqual(100, q.Count);

            // Before compression, Query should have 100% accuracy
            foreach (var quantile in _targets.Select(_ => _.Quantile))
            {
                var w = quantile * 100;
                var g = q.Query(quantile);
                Assert.AreEqual(g, w);
            }
        }

        [TestMethod]
        public void TestUncompressedSamples()
        {
            var q = QuantileStream.NewTargeted(new List<QuantileEpsilonPair> { new QuantileEpsilonPair(0.99d, 0.001d) });

            for (var i = 1; i <= 100; i++)
            {
                q.Insert(i);
            }

            Assert.AreEqual(100, q.SamplesCount);
        }

        [TestMethod]
        public void TestUncompressedOne()
        {
            var q = QuantileStream.NewTargeted(new List<QuantileEpsilonPair> { new QuantileEpsilonPair(0.99d, 0.001d) });
            q.Insert(3.14);
            var g = q.Query(0.90);
            Assert.AreEqual(3.14, g);
        }

        [TestMethod]
        public void TestDefaults()
        {
            var q = QuantileStream.NewTargeted(new List<QuantileEpsilonPair> { new QuantileEpsilonPair(0.99d, 0.001d) });
            var g = q.Query(0.99);
            Assert.AreEqual(0, g);

        }

        static double[] PopulateStream(QuantileStream stream, Random random)
        {
            var a = new double[100100];
            for (int i = 0; i < a.Length; i++)
            {
                var v = random.NormDouble();

                // Add 5% asymmetric outliers.
                if (i % 20 == 0)
                    v = v * v + 1;

                stream.Insert(v);
                a[i] = v;
            }

            return a;

        }

        void VerifyPercsWithAbsoluteEpsilon(double[] a, QuantileStream s)
        {
            Array.Sort(a);

            foreach (var target in _targets)
            {
                var n = (double)a.Length;
                var k = (int)(target.Quantile * n);
                var lower = (int)((target.Quantile - target.Epsilon) * n);
                if (lower < 1)
                    lower = 1;
                var upper = (int)Math.Ceiling((target.Quantile + target.Epsilon) * n);
                if (upper > a.Length)
                    upper = a.Length;

                var w = a[k - 1];
                var min = a[lower - 1];
                var max = a[upper - 1];

                var g = s.Query(target.Quantile);

                Assert.IsTrue(g >= min, $"q={target.Quantile}: want {w} [{min}, {max}], got {g}");
                Assert.IsTrue(g <= max, $"q={target.Quantile}: want {w} [{min}, {max}], got {g}");
            }
        }

        void VerifyLowPercsWithRelativeEpsilon(double[] a, QuantileStream s)
        {
            Array.Sort(a);

            foreach (var qu in _lowQuantiles)
            {
                var n = (double)a.Length;
                var k = (int)(qu * n);

                var lowerRank = (int)((1 - RelativeEpsilon) * qu * n);
                var upperRank = (int)(Math.Ceiling((1 + RelativeEpsilon) * qu * n));

                var w = a[k - 1];
                var min = a[lowerRank - 1];
                var max = a[upperRank - 1];

                var g = s.Query(qu);

                Assert.IsTrue(g >= min, $"q={qu}: want {w} [{min}, {max}], got {g}");
                Assert.IsTrue(g <= max, $"q={qu}: want {w} [{min}, {max}], got {g}");
            }
        }

        void VerifyHighPercsWithRelativeEpsilon(double[] a, QuantileStream s)
        {
            Array.Sort(a);

            foreach (var qu in _highQuantiles)
            {
                var n = (double)a.Length;
                var k = (int)(qu * n);

                var lowerRank = (int)((1 - (1 + RelativeEpsilon) * (1 - qu)) * n);
                var upperRank = (int)(Math.Ceiling((1 - (1 - RelativeEpsilon) * (1 - qu)) * n));
                var w = a[k - 1];
                var min = a[lowerRank - 1];
                var max = a[upperRank - 1];

                var g = s.Query(qu);

                Assert.IsTrue(g >= min, $"q={qu}: want {w} [{min}, {max}], got {g}");
                Assert.IsTrue(g <= max, $"q={qu}: want {w} [{min}, {max}], got {g}");
            }
        }
    }
}
