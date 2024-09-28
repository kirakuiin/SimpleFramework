using NUnit.Framework;
using SimpleFramework;

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
#endregion