#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using SimpleFramework.Patterns;

namespace Test.Patterns;

[TestFixture]
public class TestServiceLocator
{
    private ServiceLocator _serviceLocator;

    // 测试用的服务接口实现
    private class TestService : IGameService
    {
        public string Name { get; } = "TestService";
    }

    private class AnotherTestService : IGameService
    {
        public string Name { get; } = "AnotherTestService";
    }

    private class DerivedTestService : TestService
    {
        public new string Name { get; } = "DerivedTestService";
    }

    [SetUp]
    public void Setup()
    {
        _serviceLocator = ServiceLocator.Instance;
        _serviceLocator.Clear();
    }

    [Test]
    public void TestRegisterAndGet()
    {
        var service = new TestService();
        _serviceLocator.Register(service);

        var retrievedService = _serviceLocator.Get<TestService>();
        Assert.IsNotNull(retrievedService);
        Assert.AreEqual(service.Name, retrievedService.Name);
    }

    [Test]
    public void TestGetNonExistentService()
    {
        Assert.Throws<KeyNotFoundException>(() => _serviceLocator.Get<TestService>());
    }

    [Test]
    public void TestUnregister()
    {
        var service = new TestService();
        _serviceLocator.Register(service);
        
        _serviceLocator.UnRegister<TestService>();
        Assert.Throws<KeyNotFoundException>(() => _serviceLocator.Get<TestService>());
    }

    [Test]
    public void TestMultipleServices()
    {
        var service1 = new TestService();
        var service2 = new AnotherTestService();
        
        _serviceLocator.Register(service1);
        _serviceLocator.Register(service2);

        var retrievedService1 = _serviceLocator.Get<TestService>();
        var retrievedService2 = _serviceLocator.Get<AnotherTestService>();

        Assert.AreEqual(service1.Name, retrievedService1.Name);
        Assert.AreEqual(service2.Name, retrievedService2.Name);
    }

    [Test]
    public void TestDerivedService()
    {
        var derivedService = new DerivedTestService();
        _serviceLocator.Register(derivedService);

        // 测试通过基类获取
        var retrievedAsBase = _serviceLocator.Get<TestService>();
        Assert.IsNotNull(retrievedAsBase);
        Assert.AreEqual(nameof(TestService), retrievedAsBase.Name);

        // 测试通过派生类获取
        var retrievedAsDerived = _serviceLocator.Get<DerivedTestService>();
        Assert.IsNotNull(retrievedAsDerived);
        Assert.AreEqual(derivedService.Name, retrievedAsDerived.Name);
    }

    [Test]
    public void TestRegisterSameTypeTwice()
    {
        var service1 = new TestService();
        var service2 = new TestService();
        
        _serviceLocator.Register(service1);
        _serviceLocator.Register(service2);
        Assert.AreSame(service2, _serviceLocator.Get<TestService>());
    }

    [Test]
    public void TestUnregisterNonExistentService()
    {
        // 确保不会抛出异常
        Assert.DoesNotThrow(() => _serviceLocator.UnRegister<TestService>());
    }
} 