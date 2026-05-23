using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

[TestFixture]
public class TestTypeSignature
{
    [Test]
    public void TypeSignatureEqualityIsOrderIndependent()
    {
        var signature = new TypeSignature(typeof(TestPosition), typeof(TestVelocity), typeof(TestHealth));
        var sameTypesDifferentOrder = new TypeSignature(typeof(TestHealth), typeof(TestPosition), typeof(TestVelocity));
        var duplicateTypes = new TypeSignature(typeof(TestPosition), typeof(TestVelocity), typeof(TestPosition), typeof(TestHealth));
        var different = new TypeSignature(typeof(TestPosition), typeof(TestName));

        Assert.AreEqual(signature, sameTypesDifferentOrder);
        Assert.AreEqual(signature, duplicateTypes);
        Assert.AreEqual(signature.GetHashCode(), sameTypesDifferentOrder.GetHashCode());
        Assert.AreNotEqual(signature, different);
    }

    [Test]
    public void TypeSignatureRejectsNonComponentTypes()
    {
        Assert.Throws<ArgumentException>(() => new TypeSignature(typeof(string)));
        Assert.Throws<ArgumentException>(() => new TypeSignature(typeof(TestPosition), null!));
    }

    [Test]
    public void TypeSignatureHasAndHasAllWork()
    {
        var signature = new TypeSignature(typeof(TestPosition), typeof(TestVelocity), typeof(TestHealth));
        var required = new TypeSignature(typeof(TestVelocity), typeof(TestPosition));
        var missing = new TypeSignature(typeof(TestName));

        Assert.IsTrue(signature.Has<TestPosition>());
        Assert.IsTrue(signature.Has(typeof(TestVelocity)));
        Assert.IsFalse(signature.Has<TestName>());
        Assert.IsTrue(signature.HasAll(required));
        Assert.IsFalse(signature.HasAll(missing));
        Assert.IsTrue(signature.HasAny(new TypeSignature(typeof(TestName), typeof(TestHealth))));
        Assert.IsFalse(signature.HasAny(missing));
    }

    [Test]
    public void TypeSignatureCanEnumerateTypes()
    {
        var expected = new HashSet<Type> { typeof(TestPosition), typeof(TestVelocity), typeof(TestName) };
        var signature = new TypeSignature(expected);

        CollectionAssert.AreEquivalent(expected, signature.ToArray());
        Assert.AreEqual(expected.Count, signature.Count);
    }

    [Test]
    public void TypeSignatureToStringContainsTypeNames()
    {
        var signature = new TypeSignature(typeof(TestPosition), typeof(TestDeadTag), typeof(TestName));

        var text = signature.ToString();

        StringAssert.Contains(nameof(TestPosition), text);
        StringAssert.Contains(nameof(TestDeadTag), text);
        StringAssert.Contains(nameof(TestName), text);
    }
}
