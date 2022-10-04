using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Prometheus.Tests
{
    [TestClass]
    public sealed class StringSequenceTests
    {
        [TestMethod]
        public void EmptySequence_EqualsEmptySequence()
        {
            var empty1 = StringSequence.Empty;
            var empty2 = StringSequence.Empty;

            Assert.AreEqual(empty1, empty2);
        }

        [TestMethod]
        public void Equals_ReturnsCorrectValue()
        {
            var empty = StringSequence.Empty;
            var foo = StringSequence.From("foo");
            var foobar = StringSequence.From("foo", "bar");
            var barfoo = StringSequence.From("bar", "foo");
            var bar = StringSequence.From("bar");

            var foobarInherited = bar.InheritAndPrepend("foo");
            var barInherited = empty.InheritAndPrepend("bar");
            var foobarInherited2 = empty.InheritAndPrepend("bar").InheritAndPrepend("foo");

            var bar2 = bar.Concat(StringSequence.Empty);
            var bar3 = StringSequence.Empty.Concat(bar);

            Assert.AreEqual(foobar, foobarInherited);
            Assert.AreEqual(foobar, foobarInherited2);
            Assert.AreEqual(foobarInherited, foobarInherited2);
            Assert.AreEqual(bar, barInherited);
            Assert.AreEqual(bar, bar2);
            Assert.AreEqual(bar, bar3);

            Assert.AreNotEqual(empty, foo);
            Assert.AreNotEqual(foo, foobar);
            Assert.AreNotEqual(foobar, barfoo);
            Assert.AreNotEqual(barfoo, bar);

            CollectionAssert.AreEqual(foobar.ToArray(), foobarInherited.ToArray());
            CollectionAssert.AreEqual(foobar.ToArray(), foobarInherited2.ToArray());
            CollectionAssert.AreEqual(foobarInherited.ToArray(), foobarInherited2.ToArray());
            CollectionAssert.AreEqual(bar.ToArray(), barInherited.ToArray());
            CollectionAssert.AreEqual(bar.ToArray(), bar2.ToArray());
            CollectionAssert.AreEqual(bar.ToArray(), bar3.ToArray());

            CollectionAssert.AreNotEqual(empty.ToArray(), foo.ToArray());
            CollectionAssert.AreNotEqual(foo.ToArray(), foobar.ToArray());
            CollectionAssert.AreNotEqual(foobar.ToArray(), barfoo.ToArray());
            CollectionAssert.AreNotEqual(barfoo.ToArray(), bar.ToArray());
        }

        [TestMethod]
        public void GetEnumerator_EnumeratesExpectedValues()
        {
            var simple = StringSequence.From("one", "two", "three");
            var simpleComponents = simple.ToArray();

            Assert.AreEqual("one", simpleComponents[0]);
            Assert.AreEqual("two", simpleComponents[1]);
            Assert.AreEqual("three", simpleComponents[2]);

            var foo = StringSequence.From("foo1", "foo2");
            var inherited1 = foo.Concat(simple);
            var inheritedComponents1 = inherited1.ToArray();

            Assert.AreEqual("foo1", inheritedComponents1[0]);
            Assert.AreEqual("foo2", inheritedComponents1[1]);
            Assert.AreEqual("one", inheritedComponents1[2]);
            Assert.AreEqual("two", inheritedComponents1[3]);
            Assert.AreEqual("three", inheritedComponents1[4]);

            var bar = StringSequence.From("bar1", "bar2");
            var inherited2 = inherited1.InheritAndPrepend(bar);
            var inheritedComponents2 = inherited2.ToArray();

            Assert.AreEqual("bar1", inheritedComponents2[0]);
            Assert.AreEqual("bar2", inheritedComponents2[1]);
            Assert.AreEqual("foo1", inheritedComponents2[2]);
            Assert.AreEqual("foo2", inheritedComponents2[3]);
            Assert.AreEqual("one", inheritedComponents2[4]);
            Assert.AreEqual("two", inheritedComponents2[5]);
            Assert.AreEqual("three", inheritedComponents2[6]);
        }
    }
}
