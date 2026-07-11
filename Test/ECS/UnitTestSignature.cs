using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

    [Test]
    public void TypeSignatureDistinguishesRuntimeTypesWithIdenticalAssemblyQualifiedNames()
    {
        var firstType = CreateDynamicComponentType("CollisionModuleA");
        var secondType = CreateDynamicComponentType("CollisionModuleB");
        var first = new TypeSignature(firstType);
        var second = new TypeSignature(secondType);

        Assert.AreNotEqual(firstType, secondType);
        Assert.AreEqual(firstType.FullName, secondType.FullName);
        Assert.AreEqual(firstType.AssemblyQualifiedName, secondType.AssemblyQualifiedName);
        Assert.IsTrue(first.Has(firstType));
        Assert.IsFalse(first.Has(secondType));
        Assert.AreNotEqual(first, second);
        Assert.AreEqual(2, new HashSet<TypeSignature> { first, second }.Count);

        var combined = new TypeSignature(firstType, secondType);
        var reversed = new TypeSignature(secondType, firstType);
        Assert.AreEqual(2, combined.Count);
        Assert.IsTrue(combined.Has(firstType));
        Assert.IsTrue(combined.Has(secondType));
        Assert.AreEqual(combined, reversed);
        Assert.AreEqual(combined.GetHashCode(), reversed.GetHashCode());
    }

    internal static Type CreateDynamicComponentType(string moduleName)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("SignatureCollision"),
            AssemblyBuilderAccess.RunAndCollect);
        var module = assembly.DefineDynamicModule(moduleName);
        var builder = module.DefineType("Collision.SameComponent", TypeAttributes.Public | TypeAttributes.Class);
        builder.AddInterfaceImplementation(typeof(IComponent));
        return builder.CreateType()!;
    }
}
