# Core B+ Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Core easier to use and reason about by clarifying Domain lifecycle, registration lookup, component requirements, BindableProperty comparer behavior, and EventBus cleanup while preserving the existing lightweight style.

**Architecture:** Keep the current Domain/System/Model/Utility structure. Add small explicit APIs (`Create`, `TryGet`, `Require`, `RegisterXAs`) and fix lifecycle behavior without introducing a DI container, event bubbling, nullable migration, or broad file restructuring.

**Tech Stack:** C# / .NET 8, NUnit, existing SimpleFramework solution.

---

## File Structure

- Modify `BindableProperty.cs`: remove static comparer, use instance comparer, keep unregister behavior unchanged.
- Modify `FrameworkImpl/Event.cs`: add listener-list backed events, empty-event cleanup, `Clear`, and `RemoveEvent`.
- Modify `FrameworkImpl/Container.cs`: add `TryGet`, `Remove`, and return replaced instances from `Register`.
- Modify `Framework.cs`: add explicit Domain APIs for `RegisterXAs`, `TryGetX`, and `RequireX`.
- Modify `AbstractDomain.cs`: add `Create`, strong child ownership, lifecycle-safe repeated registration, `TryGet`/`Require`, `RegisterXAs`, and local EventBus cleanup.
- Modify `FrameworkExtension.cs`: add returned-instance `RequireModel`, `RequireSystem`, `RequireUtility`; keep existing `Require<T>` compatibility helpers.
- Modify `Test/Framework/UnitTestFrame.cs`: add focused tests for each Core behavior.
- Modify `README.md`: update user-facing Core examples and behavior notes.
- Modify `AGENTS.md`: update agent-facing sharp edges and implementation notes.

Each file keeps its current responsibility. Do not split files unless implementation reveals a direct need; this plan assumes no split is needed.

---

### Task 1: Make BindableProperty Comparer Instance-Scoped

**Files:**
- Modify: `BindableProperty.cs`
- Test: `Test/Framework/UnitTestFrame.cs`

- [ ] **Step 1: Add failing comparer tests**

Add these tests near the existing bindable property tests in `Test/Framework/UnitTestFrame.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter "TestBindableComparerIsInstanceScoped|TestBindableComparerHandlesNullValues"
```

Expected: at least `TestBindableComparerIsInstanceScoped` fails because `WithComparer` still changes static comparer shared by all `BindableProperty<int>` instances.

- [ ] **Step 3: Implement instance comparer**

In `BindableProperty.cs`, replace the static comparer with an instance field and simplify value comparison:

```csharp
public class BindableProperty<T> : IBindableProperty<T>
{
    private T _value;

    private Func<T, T, bool> _comparer = EqualityComparer<T>.Default.Equals;

    private Action<T, T> OnValueChanged { get; set; } = (_, _) => {};

    public BindableProperty(T initialValue = default) => _value = initialValue;

    public BindableProperty<T> WithComparer(Func<T, T, bool> comparer)
    {
        _comparer = comparer ?? EqualityComparer<T>.Default.Equals;
        return this;
    }

    public T Value
    {
        get => GetValue();
        set
        {
            if (_comparer(GetValue(), value)) return;

            var prev = GetValue();
            SetValue(value);
            OnValueChanged.Invoke(prev, Value);
        }
    }

    protected virtual void SetValue(T value) => _value = value;

    protected virtual T GetValue() => _value;
```

Keep the rest of the class unchanged, including `IEvent.Register`, `SetValueWithoutNotify`, register/unregister methods, and `BindablePropertyUnRegister<T>`.

- [ ] **Step 4: Run bindable tests**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter "TestBindable"
```

Expected: all bindable-related tests pass.

- [ ] **Step 5: Commit**

```powershell
git add .\BindableProperty.cs .\Test\Framework\UnitTestFrame.cs
git commit -m "fix: make bindable comparer instance scoped"
```

---

### Task 2: Add EventBus Cleanup Without Event Bubbling

**Files:**
- Modify: `FrameworkImpl/Event.cs`
- Modify: `AbstractDomain.cs`
- Test: `Test/Framework/UnitTestFrame.cs`

- [ ] **Step 1: Add failing EventBus tests**

Add these tests near `TestGlobalEvent` in `Test/Framework/UnitTestFrame.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter "TestEventBusRemovesEmptyEventAfterUnregister|TestEventBusClear|TestEventUnregisterDuringTriggerDoesNotBreakIteration|TestDomainEventsDoNotPropagateToParentOrChild"
```

Expected: compile fails because `EventBus.Clear` does not exist, or cleanup tests fail because empty event types remain registered.

- [ ] **Step 3: Implement listener-list events and cleanup**

In `FrameworkImpl/Event.cs`, update `Event<T>`:

```csharp
public class Event<T> : IEvent
{
    private readonly List<Action<T>> _listeners = new();

    public bool IsEmpty => _listeners.Count == 0;

    public IUnRegister Register(Action<T> onEvent)
    {
        _listeners.Add(onEvent);
        return new CustomUnRegister(() => UnRegister(onEvent));
    }

    public void UnRegister(Action<T> onEvent) => _listeners.Remove(onEvent);

    public void Trigger(T t)
    {
        foreach (var listener in _listeners.ToArray())
        {
            listener.Invoke(t);
        }
    }

    IUnRegister IEvent.Register(Action onEvent)
    {
        return Register(Replacement);
        void Replacement(T _) => onEvent();
    }
}
```

In `EventContainer`, add remove and clear operations:

```csharp
public void RemoveEvent<T>() where T : IEvent =>
    _events.Remove(typeof(T));

public void Clear() => _events.Clear();
```

In `EventBus`, update unregister and add clear:

```csharp
public void UnRegister<T>(Action<T> onEvent)
{
    var @event = _container.GetEvent<Event<T>>();
    if (@event == null) return;

    @event.UnRegister(onEvent);

    if (@event.IsEmpty)
    {
        _container.RemoveEvent<Event<T>>();
    }
}

public void Clear() => _container.Clear();
```

- [ ] **Step 4: Clear local Domain events on UnInitialize**

In `AbstractDomain.UnInitialize()`, after `_container.Clear();`, add:

```csharp
_eventBus.Clear();
```

Keep global events untouched.

- [ ] **Step 5: Run EventBus tests**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter "TestEvent"
```

Expected: event-related framework tests pass.

- [ ] **Step 6: Commit**

```powershell
git add .\FrameworkImpl\Event.cs .\AbstractDomain.cs .\Test\Framework\UnitTestFrame.cs
git commit -m "fix: clean up empty event registrations"
```

---

### Task 3: Extend Container as a Simple Type-Key Store

**Files:**
- Modify: `FrameworkImpl/Container.cs`
- Test: `Test/Framework/UnitTestFrame.cs`

- [ ] **Step 1: Add focused Container behavior tests through Domain**

Add these helper interfaces and types near `Utility` in `Test/Framework/UnitTestFrame.cs`:

```csharp
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
```

Add this test near `TestComponentInheritance`:

```csharp
[Test]
public void TestExplicitGenericUtilityRegistrationUsesInterfaceKey()
{
    ADomain.Instance.RegisterUtility<ITestUtility>(new InterfaceUtility(IntVal));

    Assert.IsNull(ADomain.Instance.GetUtility<InterfaceUtility>());
    Assert.AreEqual(IntVal, ADomain.Instance.GetUtility<ITestUtility>().Value);
}
```

This test currently passes, but it locks the existing generic-key behavior before adding `RegisterUtilityAs`.

- [ ] **Step 2: Add direct Container tests**

Create these tests in `Test/Framework/UnitTestFrame.cs` near other registration tests:

```csharp
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
```

- [ ] **Step 3: Run tests to verify failure**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter "TestContainerTryGetAndRemove|TestContainerRegisterReturnsPreviousInstance|TestExplicitGenericUtilityRegistrationUsesInterfaceKey"
```

Expected: compile fails because `Container.TryGet` and `Container.Remove` do not exist and `Register` returns `void`.

- [ ] **Step 4: Implement Container APIs**

In `FrameworkImpl/Container.cs`, replace `Register`, update `Get`, and add `TryGet` / `Remove`:

```csharp
public T Register<T>(T instance)
{
    Debug.Assert(instance != null, nameof(instance) + " != null");

    var key = typeof(T);
    var previous = _instances.TryGetValue(key, out var oldInstance) ? oldInstance as T : default;
    _instances[key] = instance;
    return previous;
}

public T Get<T>() where T : class
{
    return TryGet<T>(out var instance) ? instance : null!;
}

public bool TryGet<T>(out T instance) where T : class
{
    if (_instances.TryGetValue(typeof(T), out var value) && value is T result)
    {
        instance = result;
        return true;
    }

    instance = null!;
    return false;
}

public bool Remove<T>() => _instances.Remove(typeof(T));
```

Keep `GetComponents`, `Clear`, and `ToString` unchanged.

- [ ] **Step 5: Run Container tests**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter "TestContainer"
```

Expected: all Container-focused tests pass.

- [ ] **Step 6: Commit**

```powershell
git add .\FrameworkImpl\Container.cs .\Test\Framework\UnitTestFrame.cs
git commit -m "feat: add explicit container lookup helpers"
```

---

### Task 4: Add Domain Create, Strong Child Ownership, and Safe Component Registration

**Files:**
- Modify: `Framework.cs`
- Modify: `AbstractDomain.cs`
- Test: `Test/Framework/UnitTestFrame.cs`

- [ ] **Step 1: Add Domain lifecycle and registration tests**

Add these tests near the existing Domain lifecycle tests:

```csharp
[Test]
public void TestCreateReturnsIndependentDomain()
{
    var singleton = ADomain.Instance;
    var created = ADomain.Create();

    Assert.IsNotNull(created);
    Assert.AreNotSame(singleton, created);

    created.RegisterUtility(new Utility(AnoVal));
    Assert.AreEqual(AnoVal, created.GetUtility<Utility>().Value);
    Assert.AreEqual(IntVal, singleton.GetUtility<Utility>().Value);

    created.UnInitialize();

    Assert.AreSame(singleton, ADomain.GetInstance());
}

[Test]
public void TestAddChildOwnsCreatedChildLifecycle()
{
    var parent = ADomain.Create();
    var child = BDomain.Create();

    parent.AddChild(child);

    parent.UnInitialize();

    Assert.IsNull(child.Parent);
}

[Test]
public void TestRegisterModelReleasesPreviousInstance()
{
    var domain = ADomain.Create();
    var first = new LifecycleModel();
    var second = new LifecycleModel();

    domain.RegisterModel(first);
    domain.RegisterModel(second);

    Assert.AreEqual(1, first.InitializeCount);
    Assert.AreEqual(1, first.UninitializeCount);
    Assert.AreEqual(1, second.InitializeCount);
    Assert.AreEqual(0, second.UninitializeCount);

    domain.UnInitialize();

    Assert.AreEqual(1, second.UninitializeCount);
}

[Test]
public void TestRegisterSameModelInstanceDoesNotInitializeTwice()
{
    var domain = ADomain.Create();
    var model = new LifecycleModel();

    domain.RegisterModel(model);
    domain.RegisterModel(model);

    Assert.AreEqual(1, model.InitializeCount);
    Assert.AreEqual(0, model.UninitializeCount);
}

[Test]
public void TestRegisterSystemReleasesPreviousInstance()
{
    var domain = ADomain.Create();
    var first = new LifecycleSystem();
    var second = new LifecycleSystem();

    domain.RegisterSystem(first);
    domain.RegisterSystem(second);

    Assert.AreEqual(1, first.InitializeCount);
    Assert.AreEqual(1, first.UninitializeCount);
    Assert.AreEqual(1, second.InitializeCount);
    Assert.AreEqual(0, second.UninitializeCount);
}
```

Add these helper classes near the other test helper types:

```csharp
public class LifecycleModel : AbstractModel
{
    public int InitializeCount { get; private set; }
    public int UninitializeCount { get; private set; }

    protected override void OnInitialize()
    {
        InitializeCount++;
    }

    protected override void OnUninitialize()
    {
        UninitializeCount++;
    }
}

public class LifecycleSystem : AbstractSystem
{
    public int InitializeCount { get; private set; }
    public int UninitializeCount { get; private set; }

    protected override void OnInitialize()
    {
        InitializeCount++;
    }

    protected override void OnUninitialize()
    {
        UninitializeCount++;
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter "TestCreateReturnsIndependentDomain|TestAddChildOwnsCreatedChildLifecycle|TestRegisterModelReleasesPreviousInstance|TestRegisterSameModelInstanceDoesNotInitializeTwice|TestRegisterSystemReleasesPreviousInstance"
```

Expected: compile fails because `Create` does not exist; registration lifecycle tests fail until repeated registration is fixed.

- [ ] **Step 3: Extend IDomain APIs**

In `Framework.cs`, add these members to `IDomain` after existing register methods:

```csharp
void RegisterSystemAs<T>(T system) where T : ISystem;

void RegisterModelAs<T>(T model) where T : IModel;

void RegisterUtilityAs<T>(T utility) where T : IUtility;
```

Add these members after existing `GetX` methods:

```csharp
bool TryGetSystem<T>(out T system) where T : class, ISystem;

bool TryGetModel<T>(out T model) where T : class, IModel;

bool TryGetUtility<T>(out T utility) where T : class, IUtility;

T RequireSystem<T>() where T : class, ISystem;

T RequireModel<T>() where T : class, IModel;

T RequireUtility<T>() where T : class, IUtility;
```

- [ ] **Step 4: Implement Create and strong children**

In `AbstractDomain.cs`, change children storage:

```csharp
private readonly List<IDomain> _children = new();
```

Add `Create` and reuse it for `Instance`:

```csharp
public static T Instance => _domain ??= Create();

public static T GetInstance() => _domain;

public static T Create()
{
    var domain = new T();
    domain.Init();
    return domain;
}
```

Remove or stop using `BuildDomain`.

Update `UnInitialize` child release:

```csharp
foreach (var child in _children.ToList())
{
    child.UnInitialize();
}
_children.Clear();
```

Update static cleanup:

```csharp
if (ReferenceEquals(_domain, this))
{
    _domain = null;
}
```

Update `AddChild`:

```csharp
public void AddChild(IDomain child)
{
    child.SetParent(this);

    if (_children.Any(existingChild => ReferenceEquals(existingChild, child)))
    {
        return;
    }

    _children.Add(child);
}
```

Update `RemoveChild`:

```csharp
public void RemoveChild(IDomain child)
{
    if (child == null) return;

    for (var i = _children.Count - 1; i >= 0; i--)
    {
        var existingChild = _children[i];
        if (!ReferenceEquals(existingChild, child)) continue;

        _children.RemoveAt(i);

        if (ReferenceEquals(existingChild.Parent, this))
        {
            existingChild.SetParent(null);
        }
        break;
    }
}
```

- [ ] **Step 5: Implement safe registration**

In `AbstractDomain.cs`, update registration methods:

```csharp
public void RegisterSystem<TSystem>(TSystem system) where TSystem : ISystem =>
    RegisterSystemAs(system);

public void RegisterSystemAs<TSystem>(TSystem system) where TSystem : ISystem
{
    var oldSystem = _container.Get<TSystem>();
    if (ReferenceEquals(oldSystem, system)) return;

    _container.Register(system);
    oldSystem?.UnInitialize();
    system.SetDomain(this);
    system.Initialize();
}

public void RegisterModel<TModel>(TModel model) where TModel : IModel =>
    RegisterModelAs(model);

public void RegisterModelAs<TModel>(TModel model) where TModel : IModel
{
    var oldModel = _container.Get<TModel>();
    if (ReferenceEquals(oldModel, model)) return;

    _container.Register(model);
    oldModel?.UnInitialize();
    model.SetDomain(this);
    model.Initialize();
}

public void RegisterUtility<TUtility>(TUtility utility) where TUtility : IUtility =>
    RegisterUtilityAs(utility);

public void RegisterUtilityAs<TUtility>(TUtility utility) where TUtility : IUtility =>
    _container.Register(utility);
```

This keeps the old methods as the default entry point while making the `As` methods explicit aliases.

- [ ] **Step 6: Run lifecycle tests**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter "TestCreateReturnsIndependentDomain|TestAddChildOwnsCreatedChildLifecycle|TestRegisterModelReleasesPreviousInstance|TestRegisterSameModelInstanceDoesNotInitializeTwice|TestRegisterSystemReleasesPreviousInstance|TestAddChildLifeCycle|TestSetParentLifeCycle"
```

Expected: all listed tests pass.

- [ ] **Step 7: Commit**

```powershell
git add .\Framework.cs .\AbstractDomain.cs .\Test\Framework\UnitTestFrame.cs
git commit -m "feat: clarify domain lifecycle and registration"
```

---

### Task 5: Add TryGet and Require Domain APIs

**Files:**
- Modify: `AbstractDomain.cs`
- Modify: `FrameworkExtension.cs`
- Test: `Test/Framework/UnitTestFrame.cs`

- [ ] **Step 1: Add API tests**

Add these tests near existing lookup tests:

```csharp
[Test]
public void TestTryGetAndRequireModel()
{
    Assert.IsTrue(ADomain.Instance.TryGetModel<Model>(out var model));
    Assert.AreSame(ADomain.Instance.GetModel<Model>(), model);

    Assert.IsFalse(ADomain.Instance.TryGetModel<ModelNull>(out var missing));
    Assert.IsNull(missing);

    Assert.AreSame(model, ADomain.Instance.RequireModel<Model>());
    Assert.Throws<NullReferenceException>(() => ADomain.Instance.RequireModel<ModelNull>());
}

[Test]
public void TestTryGetAndRequireUtility()
{
    Assert.IsTrue(ADomain.Instance.TryGetUtility<Utility>(out var utility));
    Assert.AreSame(ADomain.Instance.GetUtility<Utility>(), utility);

    Assert.IsFalse(ADomain.Instance.TryGetUtility<ITestUtility>(out var missing));
    Assert.IsNull(missing);

    Assert.AreSame(utility, ADomain.Instance.RequireUtility<Utility>());
    Assert.Throws<NullReferenceException>(() => ADomain.Instance.RequireUtility<ITestUtility>());
}

[Test]
public void TestTryGetAndRequireSystem()
{
    Assert.IsTrue(ADomain.Instance.TryGetSystem<System>(out var system));
    Assert.AreSame(ADomain.Instance.GetSystem<System>(), system);

    Assert.IsFalse(ADomain.Instance.TryGetSystem<LifecycleSystem>(out var missing));
    Assert.IsNull(missing);

    Assert.AreSame(system, ADomain.Instance.RequireSystem<System>());
    Assert.Throws<NullReferenceException>(() => ADomain.Instance.RequireSystem<LifecycleSystem>());
}
```

- [ ] **Step 2: Add RegisterAs API tests**

Add this test near registration tests:

```csharp
[Test]
public void TestRegisterUtilityAsUsesServiceKey()
{
    ADomain.Instance.RegisterUtilityAs<ITestUtility>(new InterfaceUtility(IntVal));

    Assert.IsNull(ADomain.Instance.GetUtility<InterfaceUtility>());
    Assert.AreEqual(IntVal, ADomain.Instance.RequireUtility<ITestUtility>().Value);
}
```

- [ ] **Step 3: Run tests to verify failure**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter "TestTryGetAndRequireModel|TestTryGetAndRequireUtility|TestTryGetAndRequireSystem|TestRegisterUtilityAsUsesServiceKey"
```

Expected: compile fails until the new Domain methods are implemented.

- [ ] **Step 4: Implement TryGet and Require in AbstractDomain**

In `AbstractDomain.cs`, keep existing `GetX` behavior and add:

```csharp
public bool TryGetSystem<TSystem>(out TSystem system) where TSystem : class, ISystem
{
    system = GetSystem<TSystem>();
    return system != null;
}

public bool TryGetModel<TModel>(out TModel model) where TModel : class, IModel
{
    model = GetModel<TModel>();
    return model != null;
}

public bool TryGetUtility<TUtility>(out TUtility utility) where TUtility : class, IUtility
{
    utility = GetUtility<TUtility>();
    return utility != null;
}

public TSystem RequireSystem<TSystem>() where TSystem : class, ISystem
{
    var system = GetSystem<TSystem>();
    if (system != null) return system;
    throw new NullReferenceException($"System not found: {typeof(TSystem).Name} in {GetType().Name}.");
}

public TModel RequireModel<TModel>() where TModel : class, IModel
{
    var model = GetModel<TModel>();
    if (model != null) return model;
    throw new NullReferenceException($"Model not found: {typeof(TModel).Name} in {GetType().Name}.");
}

public TUtility RequireUtility<TUtility>() where TUtility : class, IUtility
{
    var utility = GetUtility<TUtility>();
    if (utility != null) return utility;
    throw new NullReferenceException($"Utility not found: {typeof(TUtility).Name} in {GetType().Name}.");
}
```

- [ ] **Step 5: Add returned-instance extension methods**

In `FrameworkExtension.cs`, add to the relevant extension classes:

```csharp
public static T RequireModel<T>(this IModelAccessible self) where T : class, IModel =>
    self.Domain.RequireModel<T>();
```

```csharp
public static T RequireSystem<T>(this ISystemAccessible self) where T : class, ISystem =>
    self.Domain.RequireSystem<T>();
```

```csharp
public static T RequireUtility<T>(this IUtilityAccessible self) where T : class, IUtility =>
    self.Domain.RequireUtility<T>();
```

Keep existing `Require<T>()` methods for compatibility, but change their implementation to delegate to the new returned-instance methods:

```csharp
public static void Require<T>(this IModelAccessible self) where T : class, IModel =>
    self.RequireModel<T>();
```

```csharp
public static void Require<T>(this IUtilityAccessible self) where T : class, IUtility =>
    self.RequireUtility<T>();
```

- [ ] **Step 6: Run API tests**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter "TestTryGetAndRequire|TestRegisterUtilityAsUsesServiceKey"
```

Expected: all new lookup and explicit registration tests pass.

- [ ] **Step 7: Commit**

```powershell
git add .\AbstractDomain.cs .\FrameworkExtension.cs .\Test\Framework\UnitTestFrame.cs
git commit -m "feat: add explicit domain lookup APIs"
```

---

### Task 6: Update README and AGENTS Documentation

**Files:**
- Modify: `README.md`
- Modify: `AGENTS.md`

- [ ] **Step 1: Update README Core usage examples**

In `README.md`, update the Core section to show both Domain creation modes:

```csharp
// 简单项目可以继续使用单例 Domain
var domain = GameDomain.Instance;

// 测试、多会话或工具场景可以显式创建独立 Domain
var sessionDomain = GameDomain.Create();
sessionDomain.UnInitialize();
```

Add explicit registration and required lookup examples:

```csharp
domain.RegisterUtilityAs<ITimeUtility>(new TimeUtility());

var timeUtility = domain.RequireUtility<ITimeUtility>();
```

Add event scope wording:

```text
Domain 事件默认只在当前 Domain 内触发，不会沿父子 Domain 自动传播。跨 Domain 事件应显式使用 EventBus.Global。
```

Add BindableProperty comparer wording:

```csharp
var hp = new BindableProperty<int>(100)
    .WithComparer((prev, current) => Math.Abs(prev - current) < 5);
```

State that `WithComparer` only affects the current bindable property instance.

- [ ] **Step 2: Update AGENTS Core notes**

In `AGENTS.md`, replace the old static comparer note with:

```text
- `BindableProperty<T>.WithComparer` is instance-scoped; custom comparer changes should not affect other `BindableProperty<T>` instances.
```

Add repeated registration note:

```text
- Re-registering a `System` or `Model` with the same key releases the previous instance before initializing the new instance. Re-registering the same instance should not initialize it twice.
```

Add event scope note:

```text
- Domain events are local to the Domain that registers and sends them. Parent lookup applies to components, not events. Use `EventBus.Global` for explicit cross-Domain events.
```

Add child ownership note:

```text
- `AddChild` expresses lifecycle ownership and keeps a strong child reference. `SetParent` only expresses lookup inheritance.
```

- [ ] **Step 3: Run documentation diff review**

Run:

```powershell
git diff -- README.md AGENTS.md
```

Expected: README remains user-focused; AGENTS remains agent-focused and does not duplicate large README examples.

- [ ] **Step 4: Commit**

```powershell
git add .\README.md .\AGENTS.md
git commit -m "docs: update core bplus guidance"
```

---

### Task 7: Run Full Verification

**Files:**
- No code changes expected.

- [ ] **Step 1: Restore**

Run:

```powershell
dotnet restore .\SimpleFramework.sln
```

Expected: restore succeeds.

- [ ] **Step 2: Build**

Run:

```powershell
dotnet build .\SimpleFramework.sln
```

Expected: build succeeds.

- [ ] **Step 3: Test**

Run:

```powershell
dotnet test .\SimpleFramework.sln
```

Expected: all tests pass.

- [ ] **Step 4: Review changed files**

Run:

```powershell
git status --short
git diff --stat
```

Expected: only intended files are changed. Existing unrelated user changes must not be reverted.

- [ ] **Step 5: Commit final verification adjustments if any**

If verification required small fixes, commit only those fixes:

```powershell
git add <changed-files>
git commit -m "test: cover core bplus behavior"
```

If no fixes were required, do not create an empty commit.

---

## Self-Review

Spec coverage:

- Domain lifecycle: Task 4.
- Strong child ownership: Task 4.
- Container key behavior and helpers: Task 3.
- RegisterAs, TryGet, Require APIs: Tasks 4 and 5.
- System / Model repeated registration lifecycle: Task 4.
- BindableProperty instance comparer: Task 1.
- EventBus clear and automatic empty-event cleanup: Task 2.
- Event isolation, no parent/child propagation: Task 2.
- Nullable non-goal: no implementation task.
- README / AGENTS updates: Task 6.
- Verification: Task 7.

Placeholder scan:

- This plan intentionally contains no unresolved placeholder markers.
- Every implementation task includes target files, concrete code snippets, commands, and expected outcomes.

Type consistency:

- `Create`, `RegisterXAs`, `TryGetX`, and `RequireX` names match the spec.
- Tests use existing `ADomain`, `BDomain`, `Model`, `Utility`, `System`, and new helper types defined in the same test file.
- Event tests keep the existing no-bubbling behavior.
