using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Prometheus.Tests
{
    [TestClass]
    public class ThreadSafeDoubleTests
    {
        [TestMethod]
        public void ThreadSafeDouble_Constructors()
        {
            var tsdouble = new ThreadSafeDouble();
            Assert.AreEqual(0.0, tsdouble.Value);

            tsdouble = new ThreadSafeDouble(1.42);
            Assert.AreEqual(1.42, tsdouble.Value);
        }

        [TestMethod]
        public void ThreadSafeDouble_ValueSet()
        {
            var tsdouble = new ThreadSafeDouble();
            tsdouble.Value = 3.14;
            Assert.AreEqual(3.14, tsdouble.Value);
        }

        [TestMethod]
        public void ThreadSafeDouble_Overrides()
        {
            var tsdouble = new ThreadSafeDouble(9.15);
            var equaltsdouble = new ThreadSafeDouble(9.15);
            var notequaltsdouble = new ThreadSafeDouble(10.11);

            Assert.AreEqual("9.15", tsdouble.ToString());
            Assert.IsTrue(tsdouble.Equals(equaltsdouble));
            Assert.IsFalse(tsdouble.Equals(notequaltsdouble));
            Assert.IsFalse(tsdouble.Equals(null));
            Assert.IsTrue(tsdouble.Equals(9.15));
            Assert.IsFalse(tsdouble.Equals(10.11));

            Assert.AreEqual((9.15).GetHashCode(), tsdouble.GetHashCode());
        }

        [TestMethod]
        public void ThreadSafeDouble_Add()
        {
            var tsdouble = new ThreadSafeDouble(3.10);
            tsdouble.Add(0.50);
            tsdouble.Add(2.00);
            Assert.AreEqual(5.6, tsdouble.Value);
        }
    }
}
