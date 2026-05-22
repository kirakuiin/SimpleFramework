using System;
using NUnit.Framework;
using SimpleFramework;
using SimpleFramework.FrameworkImpl;

namespace Test.Framework;

[TestFixture]
public class TestFramework
{
    private ADomain _aDomain;
    private Control _control;

    private const int IntVal = 3;
    private const int AnoVal = 4;
    private const string StrVal = "hello";
    
    [SetUp]
    public void Setup()
    {
        _aDomain = ADomain.Instance;
        
        _aDomain.RegisterModel(new Model(StrVal));
        _aDomain.RegisterSystem(new System());
        _aDomain.RegisterUtility(new Utility(IntVal));
        
        _control = new Control();
    }

    [TearDown]
    public void TearDown()
    {
        _aDomain.UnInitialize();
        BDomain.Instance.UnInitialize();
        DDomain.Instance.UnInitialize();
    }
    
    [Test]
    public void TestModelExists()
    {
        Assert.IsNotNull(_aDomain.GetModel<Model>());
        Assert.IsNull(_aDomain.GetModel<ModelNull>());
    }

    [Test]
    public void TestCommand()
    {
        Assert.AreEqual(_aDomain.GetUtility<Utility>().Value, _control.SendCommand());
    }
    
    [Test]
    public void TestQuery()
    {
        Assert.AreEqual(_aDomain.GetModel<Model>().Value.Value, _control.SendQuery());
    }

    [Test]
    public void TestBindable()
    {
        const string newWord = "world";
        _aDomain.GetModel<Model>().Value.Value = newWord;
        Assert.AreEqual(StrVal, _control.Old);
        Assert.AreEqual(newWord, _control.New);
    }

    [Test]
    public void TestBindableComparerIsInstanceScoped()
    {
        var left = new BindableProperty<int>(10)
            .WithComparer((prev, current) => Math.Abs(prev - current) < 5);
        var right = new BindableProperty<int>(10);

        var leftChanged = false;
        var rightChanged = false;

        left.Register((_, _) => leftChanged = true);
        right.Register((_, _) => rightChanged = true);

        left.Value = 12;
        right.Value = 12;

        Assert.IsFalse(leftChanged);
        Assert.IsTrue(rightChanged);
    }

    [Test]
    public void TestBindableComparerHandlesNullValues()
    {
        var property = new BindableProperty<string>(null);
        var changedCount = 0;

        property.Register((_, _) => changedCount++);

        property.Value = null;
        Assert.AreEqual(0, changedCount);

        property.Value = "hello";
        Assert.AreEqual(1, changedCount);

        property.Value = null;
        Assert.AreEqual(2, changedCount);
    }

    [Test]
    public void TestUnRegister()
    {
        _control.UnRegister.UnRegister();
        _aDomain.GetModel<Model>().Value.Value = "find";
        
        Assert.IsNull(_control.Old);
        Assert.IsNull(_control.New);
    }

    [Test]
    public void TestRegister()
    {
        var system = _aDomain.GetSystem<System>();
        
        Assert.AreEqual(System.InitVal, system.Value);

        _aDomain.GetModel<Model>().Notify();
        
        Assert.AreNotEqual(System.InitVal, system.Value);
    }

    [Test]
    public void TestParentExists()
    {
        BDomain.Instance.SetParent(ADomain.Instance);
        
        Assert.AreEqual(IntVal, BDomain.Instance.GetUtility<Utility>().Value);
    }
    
    [Test]
    public void TestParentOverride()
    {
        BDomain.Instance.SetParent(ADomain.Instance);
        BDomain.Instance.RegisterUtility(new Utility(AnoVal));
        
        Assert.AreEqual(AnoVal, BDomain.Instance.GetUtility<Utility>().Value);
    }

    [Test]
    public void TestExplicitGenericUtilityRegistrationUsesInterfaceKey()
    {
        ADomain.Instance.RegisterUtility<ITestUtility>(new InterfaceUtility(IntVal));

        Assert.IsNull(ADomain.Instance.GetUtility<InterfaceUtility>());
        Assert.AreEqual(IntVal, ADomain.Instance.GetUtility<ITestUtility>().Value);
    }

    [Test]
    public void TestContainerTryGetAndRemove()
    {
        var container = new Container();
        container.Register<IUtility>(new Utility(IntVal));

        Assert.IsTrue(container.TryGet<IUtility>(out var utility));
        Assert.AreEqual(IntVal, ((Utility)utility).Value);

        Assert.IsTrue(container.Remove<IUtility>());
        Assert.IsFalse(container.TryGet<IUtility>(out _));
    }

    [Test]
    public void TestContainerRegisterReturnsPreviousInstance()
    {
        var container = new Container();
        var first = new Utility(IntVal);
        var second = new Utility(AnoVal);

        var empty = container.Register<IUtility>(first);
        var previous = container.Register<IUtility>(second);

        Assert.IsNull(empty);
        Assert.AreSame(first, previous);
        Assert.AreSame(second, container.Get<IUtility>());
    }
    
    [Test]
    public void TestSetParentLifeCycle()
    {
        DDomain.Instance.SetParent(PDomain.Instance);
        PDomain.Instance.RegisterUtility(new Utility(IntVal));
        
        D1Domain.Instance.SetParent(DDomain.Instance);
        D2Domain.Instance.SetParent(DDomain.Instance);
        DDomain.Instance.RegisterModel(new Model("hello world"));
        
        DDomain.Instance.UnInitialize();
        
        Assert.IsNotNull(PDomain.GetInstance());
        Assert.IsNull(DDomain.GetInstance());
        Assert.IsNotNull(D1Domain.GetInstance());
        Assert.IsNotNull(D2Domain.GetInstance());
    }

    [Test]
    public void TestAddChildLifeCycle()
    {
        PDomain.Instance.AddChild(DDomain.Instance);
        PDomain.Instance.RegisterUtility(new Utility(IntVal));

        DDomain.Instance.AddChild(D1Domain.Instance);
        DDomain.Instance.AddChild(D2Domain.Instance);
        DDomain.Instance.RegisterModel(new Model("hello world"));

        Assert.AreEqual(IntVal, DDomain.Instance.GetUtility<Utility>().Value);
        Assert.AreEqual(IntVal, D1Domain.Instance.GetUtility<Utility>().Value);
        Assert.AreEqual("hello world", D2Domain.Instance.GetModel<Model>().Value.Value);

        PDomain.Instance.UnInitialize();

        Assert.IsNull(PDomain.GetInstance());
        Assert.IsNull(DDomain.GetInstance());
        Assert.IsNull(D1Domain.GetInstance());
        Assert.IsNull(D2Domain.GetInstance());
    }

    [Test]
    public void TestGlobalEvent()
    {
        var receiver = new GlobalEventReceiver();
        var receiver2 = new GlobalEventReceiver();
        var receiverUnregister = receiver.RegisterEvent();
        var receiver2Unregister = receiver2.RegisterEvent();

        try
        {
            Assert.AreEqual(0, receiver.Value);
            Assert.AreEqual(0, receiver2.Value);
        
            var value = 3;
            EventBus.Global.Send(value);
        
            Assert.AreEqual(value, receiver.Value);
            Assert.AreEqual(value, receiver2.Value);
        
            receiverUnregister.UnRegister();
        
            value = 4;
            EventBus.Global.Send(value);
            Assert.AreNotEqual(value, receiver.Value);
            Assert.AreEqual(value, receiver2.Value);
        }
        finally
        {
            receiverUnregister.UnRegister();
            receiver2Unregister.UnRegister();
        }
    }

    [Test]
    public void TestEventBusRemovesEmptyEventAfterUnregister()
    {
        var eventBus = new EventBus();
        void OnEvent(EventA _) { }

        eventBus.Register<EventA>(OnEvent);
        Assert.IsTrue(eventBus.Contains<EventA>());

        eventBus.UnRegister<EventA>(OnEvent);

        Assert.IsFalse(eventBus.Contains<EventA>());
    }

    [Test]
    public void TestEventBusClear()
    {
        var eventBus = new EventBus();
        eventBus.Register<EventA>(_ => { });

        Assert.IsTrue(eventBus.Contains<EventA>());

        eventBus.Clear();

        Assert.IsFalse(eventBus.Contains<EventA>());
    }

    [Test]
    public void TestEventBusClearMakesOldUnregisterTokenStale()
    {
        var eventBus = new EventBus();
        var called = false;
        void OnEvent(EventA _) => called = true;

        var oldUnregister = eventBus.Register<EventA>(OnEvent);
        eventBus.Clear();
        var newUnregister = eventBus.Register<EventA>(OnEvent);

        try
        {
            oldUnregister.UnRegister();
            eventBus.Send(new EventA("hello"));

            Assert.IsTrue(called);
        }
        finally
        {
            newUnregister.UnRegister();
        }
    }

    [Test]
    public void TestEventUnregisterDuringTriggerDoesNotBreakIteration()
    {
        var eventBus = new EventBus();
        IUnRegister unregister = null;
        var firstCalled = false;
        var secondCalled = false;

        unregister = eventBus.Register<EventA>(_ =>
        {
            firstCalled = true;
            unregister.UnRegister();
        });
        eventBus.Register<EventA>(_ => secondCalled = true);

        eventBus.Send(new EventA("hello"));

        Assert.IsTrue(firstCalled);
        Assert.IsTrue(secondCalled);
        Assert.IsTrue(eventBus.Contains<EventA>());
    }

    [Test]
    public void TestDomainEventsDoNotPropagateToParentOrChild()
    {
        var parentCalled = false;
        var childCalled = false;

        ADomain.Instance.RegisterEvent<EventA>(_ => parentCalled = true);
        BDomain.Instance.RegisterEvent<EventA>(_ => childCalled = true);
        BDomain.Instance.SetParent(ADomain.Instance);

        BDomain.Instance.SendEvent(new EventA("child"));

        Assert.IsFalse(parentCalled);
        Assert.IsTrue(childCalled);

        parentCalled = false;
        childCalled = false;

        ADomain.Instance.SendEvent(new EventA("parent"));

        Assert.IsTrue(parentCalled);
        Assert.IsFalse(childCalled);
    }

    [Test]
    public void TestDomainUninitializeClearsLocalEventsButKeepsGlobalEvents()
    {
        var domain = ADomain.Instance;
        var localCalled = false;
        var globalCalled = false;
        var globalUnregister = EventBus.Global.Register<int>(_ => globalCalled = true);

        try
        {
            domain.RegisterEvent<EventA>(_ => localCalled = true);

            domain.UnInitialize();
            domain.SendEvent(new EventA("local"));
            ADomain.Instance.SendEvent(new EventA("recreated"));
            EventBus.Global.Send(1);

            Assert.IsFalse(localCalled);
            Assert.IsTrue(globalCalled);
        }
        finally
        {
            globalUnregister.UnRegister();
        }
    }

    #region AbstractDomain 核心功能测试

    [Test]
    public void TestSetParentWithNull()
    {
        // 测试设置父域为 null
        BDomain.Instance.SetParent(ADomain.Instance);
        Assert.IsNotNull(BDomain.Instance.Parent);

        BDomain.Instance.SetParent(null);
        Assert.IsNull(BDomain.Instance.Parent);
    }

    [Test]
    public void TestSetParentSameParent()
    {
        // 测试设置相同的父域不应该有任何副作用
        BDomain.Instance.SetParent(ADomain.Instance);
        var parentBefore = BDomain.Instance.Parent;

        BDomain.Instance.SetParent(ADomain.Instance);
        var parentAfter = BDomain.Instance.Parent;

        Assert.AreEqual(parentBefore, parentAfter);
        Assert.AreSame(ADomain.Instance, parentAfter);
    }

    [Test]
    public void TestCycleReferenceDetection()
    {
        // 测试循环依赖检测
        Assert.Throws<ArgumentException>(() =>
        {
            ADomain.Instance.SetParent(BDomain.Instance);
            BDomain.Instance.SetParent(ADomain.Instance);
        });

    }

    [Test]
    public void TestDeepCycleReferenceDetection()
    {
        // 测试深层循环依赖检测 A -> B -> C -> A
        Assert.Throws<ArgumentException>(() =>
        {
            ADomain.Instance.SetParent(BDomain.Instance);
            BDomain.Instance.SetParent(DDomain.Instance);
            DDomain.Instance.SetParent(ADomain.Instance);
        });

    }

    [Test]
    public void TestParentChildRelationship()
    {
        // 测试父子关系的双向维护
        BDomain.Instance.SetParent(ADomain.Instance);

        Assert.AreSame(ADomain.Instance, BDomain.Instance.Parent);

        // 清理
        BDomain.Instance.SetParent(null);
        Assert.IsNull(BDomain.Instance.Parent);
    }

    [Test]
    public void TestRemoveChild()
    {
        // 测试移除子域
        BDomain.Instance.AddChild(ADomain.Instance);
        Assert.AreSame(BDomain.Instance, ADomain.Instance.Parent);

        BDomain.Instance.RemoveChild(ADomain.Instance);
        Assert.IsNull(ADomain.Instance.Parent);
    }

    [Test]
    public void TestSetParentRemoveChildOptimization()
    {
        // 测试 SetParent 和 RemoveChild 的优化，避免递归调用
        BDomain.Instance.SetParent(ADomain.Instance);
        Assert.AreSame(ADomain.Instance, BDomain.Instance.Parent);

        // 当设置新父域时，应该自动从旧父域移除
        BDomain.Instance.SetParent(DDomain.Instance);
        Assert.AreSame(DDomain.Instance, BDomain.Instance.Parent);

        // 验证旧的父子关系已经断开
        Assert.AreNotSame(ADomain.Instance, BDomain.Instance.Parent);

        // 设置为 null 时应该清除父子关系
        BDomain.Instance.SetParent(null);
        Assert.IsNull(BDomain.Instance.Parent);
    }

    [Test]
    public void TestAddChildDuplicate()
    {
        // 测试重复添加子域不会导致异常
        ADomain.Instance.AddChild(BDomain.Instance);
        ADomain.Instance.AddChild(BDomain.Instance); // 重复添加不应该出错

        // 验证父子关系仍然正确
        Assert.AreSame(ADomain.Instance, BDomain.Instance.Parent);
    }

    [Test]
    public void TestUninitializeWithChildren()
    {
        // 测试带子域的反初始化
        ADomain.Instance.AddChild(BDomain.Instance);
        BDomain.Instance.AddChild(DDomain.Instance);

        ADomain.Instance.UnInitialize();

        Assert.IsNull(ADomain.GetInstance());
        Assert.IsNull(BDomain.GetInstance());
        Assert.IsNull(DDomain.GetInstance());
    }

    [Test]
    public void TestGetInstanceAfterUninitialize()
    {
        // 测试反初始化后获取实例
        var instanceBefore = ADomain.GetInstance();
        Assert.IsNotNull(instanceBefore);

        ADomain.Instance.UnInitialize();
        var instanceAfter = ADomain.GetInstance();
        Assert.IsNull(instanceAfter);

        // 重新创建实例
        var newInstance = ADomain.Instance;
        Assert.IsNotNull(newInstance);
        Assert.AreNotSame(instanceBefore, newInstance);
    }

    [Test]
    public void TestComponentInheritance()
    {
        // 测试组件继承（子域访问父域的组件）
        ADomain.Instance.RegisterUtility(new Utility(IntVal));
        BDomain.Instance.SetParent(ADomain.Instance);

        // BDomain 应该能访问 ADomain 的 Utility
        var utility = BDomain.Instance.GetUtility<Utility>();
        Assert.IsNotNull(utility);
        Assert.AreEqual(IntVal, utility.Value);

        // BDomain 覆盖父域的 Utility
        BDomain.Instance.RegisterUtility(new Utility(AnoVal));
        var overriddenUtility = BDomain.Instance.GetUtility<Utility>();
        Assert.AreEqual(AnoVal, overriddenUtility.Value);

        // ADomain 的 Utility 不应该受影响
        var originalUtility = ADomain.Instance.GetUtility<Utility>();
        Assert.AreEqual(IntVal, originalUtility.Value);
    }

    [Test]
    public void TestToString()
    {
        // 测试 ToString 方法
        ADomain.Instance.RegisterModel(new Model(StrVal));
        ADomain.Instance.RegisterSystem(new System());
        ADomain.Instance.RegisterUtility(new Utility(IntVal));

        var result = ADomain.Instance.ToString();
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Length > 0);
    }

    #endregion
}

#region DomainDefine

public class ADomain : AbstractDomain<ADomain>
{
    protected override void Init()
    {
    }
}

public class BDomain : AbstractDomain<BDomain>
{
    protected override void Init()
    {
    }
}

public class PDomain : AbstractDomain<PDomain>
{
    protected override void Init()
    {
    }
}


public class DDomain : AbstractDomain<DDomain>
{
    protected override void Init()
    {
    }
}


public class D1Domain : AbstractDomain<D1Domain>
{
    protected override void Init()
    {
    }
}


public class D2Domain : AbstractDomain<D2Domain>
{
    protected override void Init()
    {
    }
}


public class Control : IController
{
    public IDomain Domain => ADomain.Instance;
    
    public IUnRegister UnRegister { get; private set; }

    public Control()
    {
        UnRegister = this.GetModel<Model>().Value.Register(OnValueChanged);
    }
    
    public string Old { get; private set; }
    public string New { get; private set; }

    private void OnValueChanged(string prev, string current)
    {
        Old = prev;
        New = current;
    }

    public int SendCommand()
    {
        return this.SendCommand(new Command());
    }

    public string SendQuery()
    {
        return this.SendQuery(new Query());
    }
}

public class System : AbstractSystem
{
    public const string InitVal = "init";
    public string Value { get; private set; }
    
    protected override void OnInitialize()
    {
        Value = InitVal;
        this.RegisterEvent<EventA>(OnEvent);
    }

    private void OnEvent(EventA e)
    {
        Value = e.Value;
    }
}

public class Model : AbstractModel
{
    public BindableProperty<string> Value { get; }
    
    public Model(string init)
    {
        Value = new BindableProperty<string>
        {
            Value = init
        };
    }

    public void Notify()
    {
        this.SendEvent(new EventA(Value.Value));
    }
    
    protected override void OnInitialize()
    {
    }
}

public class ModelNull : AbstractModel
{
    protected override void OnInitialize()
    {
    }
}

public class EventA
{
    public string Value { get; private set; }
    
    public EventA(string val)
    {
        Value = val;
    }
}

public class Utility : IUtility
{
    public Utility(int init)
    {
        Value = init;
    }
    
    public int Value { get; private set; }
}

public interface ITestUtility : IUtility
{
    int Value { get; }
}

public class InterfaceUtility : ITestUtility
{
    public InterfaceUtility(int value)
    {
        Value = value;
    }

    public int Value { get; }
}

public class Command : AbstractCommand<int>
{
    protected override int OnExecute()
    {
        return this.GetUtility<Utility>().Value;
    }
}

public class Query : AbstractQuery<string>
{
    protected override string OnExecute()
    {
        return this.GetModel<Model>().Value.Value;
    }
}


public class GlobalEventReceiver : IOnGlobalEvent<int>
{
    public int Value { get; private set; }
    
    public void OnEvent(int @event)
    {
        Value = @event;
    }
}

#endregion
