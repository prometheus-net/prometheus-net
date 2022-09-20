using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Prometheus.Tests
{
    [TestClass]
    public class ThreadSafeLongTests
    {
        [TestMethod]
        public void ThreadSafeLong_Constructors()
        {
            var tsdouble = new ThreadSafeLong();
            Assert.AreEqual(0L, tsdouble.Value);

            tsdouble = new ThreadSafeLong(1L);
            Assert.AreEqual(1L, tsdouble.Value);
        }

        [TestMethod]
        public void ThreadSafeLong_ValueSet()
        {
            var tsdouble = new ThreadSafeLong();
            tsdouble.Value = 3L;
            Assert.AreEqual(3L, tsdouble.Value);
        }

        [TestMethod]
        public void ThreadSafeLong_Overrides()
        {
            var tsdouble = new ThreadSafeLong(9L);
            var equaltsdouble = new ThreadSafeLong(9L);
            var notequaltsdouble = new ThreadSafeLong(10L);

            Assert.AreEqual("9", tsdouble.ToString());
            Assert.IsTrue(tsdouble.Equals(equaltsdouble));
            Assert.IsFalse(tsdouble.Equals(notequaltsdouble));
            Assert.IsFalse(tsdouble.Equals(null));
            Assert.IsTrue(tsdouble.Equals(9L));
            Assert.IsFalse(tsdouble.Equals(10L));

            Assert.AreEqual((9L).GetHashCode(), tsdouble.GetHashCode());
        }

        [TestMethod]
        public void ThreadSafeLong_Add()
        {
            var tsdouble = new ThreadSafeLong(3L);
            tsdouble.Add(2L);
            tsdouble.Add(5L);
            Assert.AreEqual(10L, tsdouble.Value);
        }
    }
}
