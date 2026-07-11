# Round 6 Public Declaration Inventory

本附录是 Round 6 public-declaration 审查的逐条证据账本。定位集合来自当前最终源码上的精确命令，处置与理由代码按声明所在类型和契约维度填写：

```powershell
rg -n "^public |^\s+public " -g '*.cs' -g '!Test/**' -g '!**/bin/**' -g '!**/obj/**'
```

## 处置口径

- **已核验**：签名、命名、空值/异常、所有权或模块边界与现有契约一致，无需修改。
- **本轮修正**：Round 6 实际修改了该 public API 的签名或直接契约。
- **保留**：识别到可讨论的旧命名或可变视图，但因兼容性或已实现接口契约明确不改。

## 理由代码

| 代码 | 已核验的契约维度 |
| --- | --- |
| R01 | Core Domain 的注册、查找、命令/查询与生命周期语义。 |
| R02 | 事件/订阅句柄、资源所有权、清理与释放形状。 |
| R03 | 集合暴露、快照/只读边界、增删与计数语义。 |
| R04 | Maths 值语义、运算符、工厂及输入验证。 |
| R05 | Utility/Toolkit 的文件、序列化、配置、日志与空值约定。 |
| R06 | 异步、取消参数位置、取消传播及 async disposal。 |
| R07 | ECS 的 World/Entity 身份、结构变更、查询与系统顺序。 |
| R08 | Net 的会话、传输、消息、结果、诊断和流转边界。 |
| R09 | Discovery 的广告、schema、扫描、浏览器与数据包契约。 |
| R10 | 常量、record/enum、options、attribute、result/status 等数据契约。 |
| R11 | 旧拼写（UnRegister/RegisterAble/IReadonly）；无正确性收益时保留。 |
| R12 | FrameworkImpl 公开实现助手与 Core 接口的一致性。 |
| R13 | 构造/注册/查找/Try/Resolve 的验证与失败语义。 |
| R14 | Patterns 的单例、服务定位、池、消息、黑板和状态机生命周期。 |
| R15 | GDExt 的 Godot 条件 API、节点所有权与信道常量。 |
| R16 | 实现的 .NET 集合接口所要求的公开成员和可变视图。 |

组合代码表示声明同时按多个维度核验，不是无差别模板。例如 Discovery 异步扫描绑定 R09+R06，集合接口 Keys/Values 绑定 R03+R16。

## 可重算汇总

| 模块 | 条数 |
| --- | ---: |
| Core | 98 |
| FrameworkImpl | 46 |
| Collections | 44 |
| Maths | 66 |
| Utility | 85 |
| Toolkit | 13 |
| Patterns | 115 |
| ECS | 159 |
| Net | 422 |
| GDExt | 13 |
| **合计** | **1,061** |

## 逐条账本

| 定位 | 声明标识 | 处置 | 理由代码 |
| --- | --- | --- | --- |
| `Framework.cs:9` | `public interface IDomain` | 已核验 | R01 |
| `Framework.cs:223` | `public interface IController : ISystemAccessible, IModelAccessible, IUtilityAccessible, IEventRegistrable, IQueryTr...` | 已核验 | R01 |
| `Framework.cs:230` | `public interface ISystem : IDomainConfigurable, IModelAccessible,` | 已核验 | R01 |
| `Framework.cs:238` | `public interface IModel : IDomainConfigurable, IUtilityAccessible,` | 已核验 | R01 |
| `Framework.cs:246` | `public interface IUtility` | 已核验 | R01 |
| `Framework.cs:254` | `public interface ICommand : IDomainConfigurable,` | 已核验 | R01 |
| `Framework.cs:268` | `public interface ICommand<out TResult> : IDomainConfigurable,` | 已核验 | R01+R10 |
| `Framework.cs:282` | `public interface IQuery<out TResult> : IDomainConfigurable,` | 已核验 | R01+R10 |
| `Framework.cs:295` | `public interface IUnRegister : IDisposable` | 保留 | R01+R02+R13+R11 |
| `BindableProperty.cs:10` | `public interface IReadonlyBindableProperty<out T> : IEvent` | 保留 | R01+R11 |
| `BindableProperty.cs:42` | `public interface IBindableProperty<T> : IReadonlyBindableProperty<T>` | 保留 | R01+R11 |
| `BindableProperty.cs:60` | `public class BindableProperty<T> : IBindableProperty<T>` | 已核验 | R01 |
| `BindableProperty.cs:72` | `public BindableProperty(T initialValue = default!) => _value = initialValue;` | 已核验 | R01 |
| `BindableProperty.cs:79` | `public BindableProperty<T> WithComparer(Func<T, T, bool>? comparer)` | 已核验 | R01 |
| `BindableProperty.cs:86` | `public T Value` | 已核验 | R01 |
| `BindableProperty.cs:118` | `public void SetValueWithoutNotify(T value) => SetValue(value);` | 已核验 | R01 |
| `BindableProperty.cs:121` | `public IUnRegister RegisterWithNotify(Action<T, T> onValueChanged)` | 保留 | R01+R02+R13+R11 |
| `BindableProperty.cs:129` | `public IUnRegister Register(Action<T, T> onValueChanged)` | 保留 | R01+R02+R13+R11 |
| `BindableProperty.cs:136` | `public void UnRegister(Action<T, T> onValueChanged)` | 保留 | R01+R02+R13+R11 |
| `BindableProperty.cs:145` | `public override string ToString() => Value?.ToString() ?? string.Empty;` | 已核验 | R01 |
| `BindableProperty.cs:158` | `public BindablePropertyUnRegister(IReadonlyBindableProperty<T> property, Action<T, T> onValueChanged)` | 保留 | R01+R02+R13+R11 |
| `BindableProperty.cs:164` | `public void UnRegister()` | 保留 | R01+R02+R13+R11 |
| `FrameworkExtension.cs:8` | `public static class ModelReadableExtensions` | 已核验 | R01 |
| `FrameworkExtension.cs:16` | `public static T? GetModel<T>(this IModelAccessible self) where T : class, IModel =>` | 已核验 | R01+R13 |
| `FrameworkExtension.cs:26` | `public static T RequireModel<T>(this IModelAccessible self) where T : class, IModel =>` | 已核验 | R01+R13 |
| `FrameworkExtension.cs:35` | `public static void Require<T>(this IModelAccessible self) where T : class, IModel` | 已核验 | R01+R13 |
| `FrameworkExtension.cs:44` | `public static class SystemReadableExtensions` | 已核验 | R01 |
| `FrameworkExtension.cs:52` | `public static T? GetSystem<T>(this ISystemAccessible self) where T : class, ISystem =>` | 已核验 | R01+R13 |
| `FrameworkExtension.cs:62` | `public static T RequireSystem<T>(this ISystemAccessible self) where T : class, ISystem =>` | 已核验 | R01+R13 |
| `FrameworkExtension.cs:69` | `public static class UtilityReadableExtensions` | 已核验 | R01 |
| `FrameworkExtension.cs:77` | `public static T? GetUtility<T>(this IUtilityAccessible self) where T : class, IUtility =>` | 已核验 | R01+R13 |
| `FrameworkExtension.cs:87` | `public static T RequireUtility<T>(this IUtilityAccessible self) where T : class, IUtility =>` | 已核验 | R01+R13 |
| `FrameworkExtension.cs:96` | `public static void Require<T>(this IUtilityAccessible self) where T : class, IUtility` | 已核验 | R01+R13 |
| `FrameworkExtension.cs:105` | `public static class RegisterAbleExtensions` | 保留 | R01+R13+R11 |
| `FrameworkExtension.cs:114` | `public static IUnRegister RegisterEvent<T>(this IEventRegistrable self, Action<T> action) =>` | 保留 | R01+R02+R13+R11 |
| `FrameworkExtension.cs:123` | `public static void UnRegisterEvent<T>(this IEventRegistrable self, Action<T> action) =>` | 保留 | R01+R02+R13+R11 |
| `FrameworkExtension.cs:130` | `public static class SendEventAbleExtensions` | 已核验 | R01 |
| `FrameworkExtension.cs:137` | `public static void SendEvent<T>(this IEventTransmittable self) where T : new() =>` | 已核验 | R01 |
| `FrameworkExtension.cs:146` | `public static void SendEvent<T>(this IEventTransmittable self, T @event) =>` | 已核验 | R01+R02 |
| `FrameworkExtension.cs:153` | `public static class GlobalEventsExtensions` | 已核验 | R01 |
| `FrameworkExtension.cs:161` | `public static IUnRegister RegisterEvent<T>(this IOnGlobalEvent<T> self)` | 保留 | R01+R02+R13+R11 |
| `FrameworkExtension.cs:169` | `public static void UnRegisterEvent<T>(this IOnGlobalEvent<T> self)` | 保留 | R01+R02+R13+R11 |
| `FrameworkExtension.cs:176` | `public static class CommandExtensions` | 已核验 | R01 |
| `FrameworkExtension.cs:183` | `public static void SendCommand<T>(this ICommandTransmittable self) where T : ICommand, new() => self.Domain.SendCom...` | 已核验 | R01 |
| `FrameworkExtension.cs:191` | `public static void SendCommand<T>(this ICommandTransmittable self, T command) where T : ICommand` | 已核验 | R01 |
| `FrameworkExtension.cs:201` | `public static TResult SendCommand<TResult>(this ICommandTransmittable self, ICommand<TResult> command) => self.Doma...` | 已核验 | R01+R10 |
| `FrameworkExtension.cs:207` | `public static class QueryExtensions` | 已核验 | R01 |
| `FrameworkExtension.cs:216` | `public static TResult SendQuery<TResult>(this IQueryTransmittable self, IQuery<TResult> query) => self.Domain.SendQ...` | 已核验 | R01+R10 |
| `AbstractSystem.cs:8` | `public abstract class AbstractSystem : ISystem` | 已核验 | R01 |
| `AbstractSystem.cs:11` | `public IDomain Domain { get; private set; } = default!;` | 已核验 | R01 |
| `AbstractSystem.cs:14` | `public void SetDomain(IDomain domain)` | 已核验 | R01 |
| `AbstractQuery.cs:6` | `public abstract class AbstractQuery<TResult> : IQuery<TResult>` | 已核验 | R01+R10 |
| `AbstractQuery.cs:9` | `public IDomain Domain { get; private set; } = default!;` | 已核验 | R01 |
| `AbstractQuery.cs:12` | `public void SetDomain(IDomain domain) => Domain = domain;` | 已核验 | R01 |
| `AbstractQuery.cs:15` | `public TResult Execute() => OnExecute();` | 已核验 | R01+R10 |
| `AbstractDomain.cs:8` | `public abstract class AbstractDomain<T> : IDomain where T : AbstractDomain<T>, new()` | 已核验 | R01 |
| `AbstractDomain.cs:29` | `public static T Instance => _domain ??= Create();` | 已核验 | R01+R13 |
| `AbstractDomain.cs:34` | `public static T? GetInstance() => _domain;` | 已核验 | R01+R13 |
| `AbstractDomain.cs:40` | `public static T Create()` | 已核验 | R01+R13 |
| `AbstractDomain.cs:53` | `public void UnInitialize()` | 已核验 | R01 |
| `AbstractDomain.cs:126` | `public IDomain? Parent => _parent?.TryGetTarget(out var parent) == true ? parent : null;` | 已核验 | R01+R13 |
| `AbstractDomain.cs:129` | `public void SetParent(IDomain? parent)` | 已核验 | R01 |
| `AbstractDomain.cs:177` | `public void AddChild(IDomain child)` | 已核验 | R01 |
| `AbstractDomain.cs:191` | `public void RemoveChild(IDomain child)` | 已核验 | R01 |
| `AbstractDomain.cs:212` | `public void RegisterSystem<TSystem>(TSystem system) where TSystem : ISystem` | 已核验 | R01+R13 |
| `AbstractDomain.cs:216` | `public void RegisterSystemAs<TSystem>(TSystem system) where TSystem : ISystem` | 已核验 | R01+R13 |
| `AbstractDomain.cs:220` | `public void RegisterModel<TModel>(TModel model) where TModel : IModel` | 已核验 | R01+R13 |
| `AbstractDomain.cs:224` | `public void RegisterModelAs<TModel>(TModel model) where TModel : IModel` | 已核验 | R01+R13 |
| `AbstractDomain.cs:298` | `public void RegisterUtility<TUtility>(TUtility utility) where TUtility : IUtility` | 已核验 | R01+R13 |
| `AbstractDomain.cs:302` | `public void RegisterUtilityAs<TUtility>(TUtility utility) where TUtility : IUtility` | 已核验 | R01+R13 |
| `AbstractDomain.cs:375` | `public TSystem? GetSystem<TSystem>() where TSystem : class, ISystem` | 已核验 | R01+R13 |
| `AbstractDomain.cs:389` | `public bool TryGetSystem<TSystem>(out TSystem? system) where TSystem : class, ISystem` | 已核验 | R01+R13 |
| `AbstractDomain.cs:401` | `public TSystem RequireSystem<TSystem>() where TSystem : class, ISystem` | 已核验 | R01+R13 |
| `AbstractDomain.cs:409` | `public TModel? GetModel<TModel>() where TModel : class, IModel` | 已核验 | R01+R13 |
| `AbstractDomain.cs:423` | `public bool TryGetModel<TModel>(out TModel? model) where TModel : class, IModel` | 已核验 | R01+R13 |
| `AbstractDomain.cs:435` | `public TModel RequireModel<TModel>() where TModel : class, IModel` | 已核验 | R01+R13 |
| `AbstractDomain.cs:443` | `public TUtility? GetUtility<TUtility>() where TUtility : class, IUtility` | 已核验 | R01+R13 |
| `AbstractDomain.cs:457` | `public bool TryGetUtility<TUtility>(out TUtility? utility) where TUtility : class, IUtility` | 已核验 | R01+R13 |
| `AbstractDomain.cs:469` | `public TUtility RequireUtility<TUtility>() where TUtility : class, IUtility` | 已核验 | R01+R13 |
| `AbstractDomain.cs:477` | `public IUnRegister RegisterEvent<TEvent>(Action<TEvent> onEvent) => _eventBus.Register(onEvent);` | 保留 | R01+R02+R13+R11 |
| `AbstractDomain.cs:480` | `public void UnRegisterEvent<TEvent>(Action<TEvent> onEvent) => _eventBus.UnRegister(onEvent);` | 保留 | R01+R02+R13+R11 |
| `AbstractDomain.cs:483` | `public void SendEvent<TEvent>() where TEvent : new() => _eventBus.Send<TEvent>();` | 已核验 | R01 |
| `AbstractDomain.cs:486` | `public void SendEvent<TEvent>(TEvent @event) => _eventBus.Send(@event);` | 已核验 | R01+R02 |
| `AbstractDomain.cs:489` | `public void SendCommand<TCommand>(TCommand command) where TCommand : ICommand =>` | 已核验 | R01 |
| `AbstractDomain.cs:504` | `public TResult SendCommand<TResult>(ICommand<TResult> command) => ExecuteCommand(command);` | 已核验 | R01+R10 |
| `AbstractDomain.cs:519` | `public TResult SendQuery<TResult>(IQuery<TResult> query) => ExecuteQuery(query);` | 已核验 | R01+R10 |
| `AbstractDomain.cs:534` | `public override string ToString() => _container.ToString();` | 已核验 | R01 |
| `AbstractCommand.cs:6` | `public abstract class AbstractCommand : ICommand` | 已核验 | R01 |
| `AbstractCommand.cs:9` | `public IDomain Domain { get; private set; } = default!;` | 已核验 | R01 |
| `AbstractCommand.cs:12` | `public void SetDomain(IDomain domain) => Domain = domain;` | 已核验 | R01 |
| `AbstractCommand.cs:15` | `public void Execute() => OnExecute();` | 已核验 | R01 |
| `AbstractCommand.cs:27` | `public abstract class AbstractCommand<TResult> : ICommand<TResult>` | 已核验 | R01+R10 |
| `AbstractCommand.cs:30` | `public IDomain Domain { get; private set; } = default!;` | 已核验 | R01 |
| `AbstractCommand.cs:33` | `public void SetDomain(IDomain domain) => Domain = domain;` | 已核验 | R01 |
| `AbstractCommand.cs:36` | `public TResult Execute() => OnExecute();` | 已核验 | R01+R10 |
| `AbstractModel.cs:7` | `public abstract class AbstractModel : IModel` | 已核验 | R01 |
| `AbstractModel.cs:10` | `public IDomain Domain { get; private set; } = default!;` | 已核验 | R01 |
| `AbstractModel.cs:13` | `public void SetDomain(IDomain domain)` | 已核验 | R01 |
| `FrameworkImpl/Traits.cs:6` | `public interface IDomainConfigurable` | 已核验 | R12 |
| `FrameworkImpl/Traits.cs:18` | `public interface IDomainAccessible` | 已核验 | R12 |
| `FrameworkImpl/Traits.cs:29` | `public interface IModelAccessible : IDomainAccessible` | 已核验 | R12 |
| `FrameworkImpl/Traits.cs:36` | `public interface ISystemAccessible : IDomainAccessible` | 已核验 | R12 |
| `FrameworkImpl/Traits.cs:43` | `public interface IUtilityAccessible : IDomainAccessible` | 已核验 | R12 |
| `FrameworkImpl/Traits.cs:50` | `public interface IEventRegistrable : IDomainAccessible` | 已核验 | R12 |
| `FrameworkImpl/Traits.cs:57` | `public interface IEventTransmittable : IDomainAccessible` | 已核验 | R12 |
| `FrameworkImpl/Traits.cs:64` | `public interface ICommandTransmittable : IDomainAccessible` | 已核验 | R12 |
| `FrameworkImpl/Traits.cs:71` | `public interface IQueryTransmittable : IDomainAccessible` | 已核验 | R12 |
| `FrameworkImpl/Traits.cs:78` | `public interface IConstructable` | 已核验 | R12 |
| `FrameworkImpl/FrameworkDefine.cs:8` | `public static class Log` | 已核验 | R12 |
| `FrameworkImpl/FrameworkDefine.cs:16` | `public static void Debug(string message)` | 已核验 | R12 |
| `FrameworkImpl/FrameworkDefine.cs:25` | `public static void Info(string message)` | 已核验 | R12 |
| `FrameworkImpl/FrameworkDefine.cs:35` | `public static void Error(string message, Exception? exception = null)` | 已核验 | R12+R10 |
| `FrameworkImpl/Event.cs:6` | `public interface IEvent` | 已核验 | R12 |
| `FrameworkImpl/Event.cs:20` | `public interface IOnGlobalEvent<in T>` | 已核验 | R12 |
| `FrameworkImpl/Event.cs:32` | `public class CustomUnRegister : IUnRegister` | 保留 | R12+R02+R13+R11 |
| `FrameworkImpl/Event.cs:40` | `public CustomUnRegister(Action onUnRegister)` | 保留 | R12+R02+R13+R11 |
| `FrameworkImpl/Event.cs:49` | `public void UnRegister()` | 保留 | R12+R02+R13+R11 |
| `FrameworkImpl/Event.cs:60` | `public class Event<T> : IEvent` | 已核验 | R12 |
| `FrameworkImpl/Event.cs:67` | `public bool IsEmpty => _listeners.Count == 0;` | 已核验 | R12+R03 |
| `FrameworkImpl/Event.cs:74` | `public IUnRegister Register(Action<T> onEvent)` | 保留 | R12+R02+R13+R11 |
| `FrameworkImpl/Event.cs:84` | `public void UnRegister(Action<T> onEvent) => _listeners.Remove(onEvent);` | 保留 | R12+R02+R03+R13+R11 |
| `FrameworkImpl/Event.cs:90` | `public void Trigger(T t)` | 已核验 | R12 |
| `FrameworkImpl/Event.cs:108` | `public class EventContainer` | 已核验 | R12 |
| `FrameworkImpl/Event.cs:117` | `public T AddEvent<T>() where T : IEvent, new()` | 已核验 | R12 |
| `FrameworkImpl/Event.cs:130` | `public T GetEvent<T>() where T : IEvent =>` | 已核验 | R12+R13 |
| `FrameworkImpl/Event.cs:137` | `public void RemoveEvent<T>() where T : IEvent =>` | 已核验 | R12 |
| `FrameworkImpl/Event.cs:143` | `public void Clear() => _events.Clear();` | 已核验 | R12+R02 |
| `FrameworkImpl/Event.cs:149` | `public class EventBus` | 已核验 | R12 |
| `FrameworkImpl/Event.cs:156` | `public static readonly EventBus Global = new();` | 已核验 | R12+R10 |
| `FrameworkImpl/Event.cs:162` | `public void Send<T>() where T : new() =>` | 已核验 | R12 |
| `FrameworkImpl/Event.cs:170` | `public void Send<T>(T e) => _container.GetEvent<Event<T>>()?.Trigger(e);` | 已核验 | R12+R13 |
| `FrameworkImpl/Event.cs:177` | `public bool Contains<T>() => _container.GetEvent<Event<T>>() != null;` | 已核验 | R12+R13 |
| `FrameworkImpl/Event.cs:185` | `public IUnRegister Register<T>(Action<T> onEvent)` | 保留 | R12+R02+R13+R11 |
| `FrameworkImpl/Event.cs:204` | `public void UnRegister<T>(Action<T> onEvent)` | 保留 | R12+R02+R13+R11 |
| `FrameworkImpl/Event.cs:222` | `public void Clear() => _container.Clear();` | 已核验 | R12+R02 |
| `FrameworkImpl/Container.cs:9` | `public class Container` | 已核验 | R12 |
| `FrameworkImpl/Container.cs:21` | `public T Register<T>(T instance)` | 已核验 | R12+R13 |
| `FrameworkImpl/Container.cs:36` | `public T? Get<T>() where T : class` | 已核验 | R12+R13 |
| `FrameworkImpl/Container.cs:46` | `public object? Get(Type key)` | 已核验 | R12+R13 |
| `FrameworkImpl/Container.cs:57` | `public bool TryGet<T>(out T? instance) where T : class` | 已核验 | R12+R13 |
| `FrameworkImpl/Container.cs:74` | `public bool Remove<T>() => _instances.Remove(typeof(T));` | 已核验 | R12+R03 |
| `FrameworkImpl/Container.cs:81` | `public IEnumerable<T> GetComponents<T>()` | 已核验 | R12+R03+R13 |
| `FrameworkImpl/Container.cs:89` | `public void Clear() => _instances.Clear();` | 已核验 | R12+R02 |
| `FrameworkImpl/Container.cs:91` | `public override string ToString()` | 已核验 | R12 |
| `Collections/DefaultDict.cs:11` | `public class DefaultDict<TK, TV> :` | 已核验 | R03+R16 |
| `Collections/DefaultDict.cs:25` | `public DefaultDict(Func<TV> initCallback, IEqualityComparer<TK>? comparer = null)` | 已核验 | R03+R16 |
| `Collections/DefaultDict.cs:35` | `public IEqualityComparer<TK> Comparer => _delegate.Comparer;` | 已核验 | R03+R16 |
| `Collections/DefaultDict.cs:38` | `public IEnumerator<KeyValuePair<TK, TV>> GetEnumerator()` | 已核验 | R03+R16+R13 |
| `Collections/DefaultDict.cs:49` | `public void Clear()` | 已核验 | R03+R16+R02 |
| `Collections/DefaultDict.cs:77` | `public int Count => _delegate.Count;` | 已核验 | R03+R16 |
| `Collections/DefaultDict.cs:80` | `public void Add(TK key, TV value)` | 已核验 | R03+R16 |
| `Collections/DefaultDict.cs:86` | `public bool Remove(TK key)` | 已核验 | R03+R16 |
| `Collections/DefaultDict.cs:92` | `public bool ContainsKey(TK key)` | 已核验 | R03+R16 |
| `Collections/DefaultDict.cs:107` | `public bool TryGetValue(TK key, [MaybeNullWhen(false)] out TV value)` | 已核验 | R03+R16+R13 |
| `Collections/DefaultDict.cs:115` | `public TV this[TK key]` | 已核验 | R03+R16 |
| `Collections/DefaultDict.cs:135` | `public ICollection<TK> Keys => _delegate.Keys;` | 保留 | R03+R16 |
| `Collections/DefaultDict.cs:138` | `public ICollection<TV> Values => _delegate.Values;` | 保留 | R03+R16 |
| `Collections/Counter.cs:9` | `public class Counter<T>` | 已核验 | R03+R16 |
| `Collections/Counter.cs:20` | `public Counter(IEnumerable<T> sequence, IEqualityComparer<T>? comparer = null)` | 已核验 | R03+R16 |
| `Collections/Counter.cs:33` | `public Counter(Counter<T> other)` | 已核验 | R03+R16 |
| `Collections/Counter.cs:45` | `public Counter()` | 已核验 | R03+R16 |
| `Collections/Counter.cs:54` | `public Counter(IEqualityComparer<T>? comparer)` | 已核验 | R03+R16 |
| `Collections/Counter.cs:62` | `public IEqualityComparer<T> Comparer => _delegate.Comparer;` | 已核验 | R03+R16 |
| `Collections/Counter.cs:65` | `public IEnumerator<KeyValuePair<T, long>> GetEnumerator()` | 已核验 | R03+R16+R13 |
| `Collections/Counter.cs:76` | `public void Clear()` | 已核验 | R03+R16+R02 |
| `Collections/Counter.cs:115` | `public int Count => _delegate.Count;` | 已核验 | R03+R16 |
| `Collections/Counter.cs:118` | `public void Add(T key, long value)` | 已核验 | R03+R16 |
| `Collections/Counter.cs:124` | `public bool Remove(T key)` | 已核验 | R03+R16 |
| `Collections/Counter.cs:130` | `public bool ContainsKey(T key)` | 已核验 | R03+R16 |
| `Collections/Counter.cs:136` | `public bool TryGetValue(T key, out long value)` | 已核验 | R03+R16+R13 |
| `Collections/Counter.cs:144` | `public long this[T key]` | 已核验 | R03+R16 |
| `Collections/Counter.cs:155` | `public ICollection<T> Keys => _delegate.Keys;` | 保留 | R03+R16 |
| `Collections/Counter.cs:158` | `public ICollection<long> Values => _delegate.Values;` | 保留 | R03+R16 |
| `Collections/Counter.cs:166` | `public IEnumerable<KeyValuePair<T, long>> MostCommon(ulong n = ulong.MaxValue)` | 已核验 | R03+R16 |
| `Collections/Counter.cs:205` | `public long Total()` | 已核验 | R03+R16 |
| `Collections/Counter.cs:214` | `public IEnumerable<T> Elements()` | 已核验 | R03+R16 |
| `Collections/Counter.cs:230` | `public void Update(Counter<T> other)` | 已核验 | R03+R16 |
| `Collections/Counter.cs:245` | `public void Subtract(Counter<T> other)` | 已核验 | R03+R16 |
| `Collections/Counter.cs:257` | `public static Counter<T> operator -(Counter<T> origin)` | 已核验 | R03+R16 |
| `Collections/Counter.cs:275` | `public static Counter<T> operator -(Counter<T> a, Counter<T> b)` | 已核验 | R03+R16 |
| `Collections/Counter.cs:294` | `public static Counter<T> operator +(Counter<T> a, Counter<T> b)` | 已核验 | R03+R16 |
| `Collections/Counter.cs:308` | `public static bool operator >(Counter<T> a, Counter<T> b)` | 已核验 | R03+R16 |
| `Collections/Counter.cs:315` | `public static bool operator <(Counter<T> a, Counter<T> b)` | 已核验 | R03+R16 |
| `Collections/Counter.cs:322` | `public static bool operator >=(Counter<T> a, Counter<T> b)` | 已核验 | R03+R16 |
| `Collections/Counter.cs:329` | `public static bool operator <=(Counter<T> a, Counter<T> b)` | 已核验 | R03+R16 |
| `Collections/Counter.cs:356` | `public bool Equals(Counter<T>? other)` | 已核验 | R03+R16 |
| `Collections/Counter.cs:365` | `public override bool Equals(object? obj)` | 已核验 | R03+R16 |
| `Collections/Counter.cs:371` | `public override int GetHashCode()` | 已核验 | R03+R16+R13 |
| `Maths/Matrix.cs:6` | `public readonly struct Matrix2D` | 已核验 | R04 |
| `Maths/Matrix.cs:13` | `public readonly double M11;` | 已核验 | R04 |
| `Maths/Matrix.cs:18` | `public readonly double M12;` | 已核验 | R04 |
| `Maths/Matrix.cs:23` | `public readonly double M21;` | 已核验 | R04 |
| `Maths/Matrix.cs:28` | `public readonly double M22;` | 已核验 | R04 |
| `Maths/Matrix.cs:37` | `public Matrix2D(double m11, double m12, double m21, double m22)` | 已核验 | R04 |
| `Maths/Matrix.cs:48` | `public double Determinant => M11 * M22 - M12 * M21;` | 已核验 | R04 |
| `Maths/Matrix.cs:53` | `public static Matrix2D Identity => new(1, 0, 0, 1);` | 已核验 | R04 |
| `Maths/Matrix.cs:60` | `public Point Transform(Point p)` | 已核验 | R04 |
| `Maths/Matrix.cs:72` | `public Matrix2D Inverse()` | 已核验 | R04 |
| `Maths/Matrix.cs:97` | `public double Dot(Matrix2D other)` | 已核验 | R04 |
| `Maths/Matrix.cs:109` | `public static Matrix2D CreateScale(double sx, double sy)` | 已核验 | R04 |
| `Maths/Matrix.cs:119` | `public static Matrix2D CreateScale(double scale)` | 已核验 | R04 |
| `Maths/Matrix.cs:127` | `public static Matrix2D operator *(Matrix2D a, Matrix2D b)` | 已核验 | R04 |
| `Maths/Matrix.cs:139` | `public static Matrix2D operator *(Matrix2D m, double s)` | 已核验 | R04 |
| `Maths/Matrix.cs:149` | `public static Matrix2D operator *(double s, Matrix2D m) => m * s;` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:13` | `public readonly struct HexOrientation` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:16` | `public Matrix2D Forward { get; }` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:18` | `public Matrix2D Inverse { get; }` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:20` | `public double StartAngle { get; }` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:30` | `public HexOrientation(double f0, double f1, double f2, double f3, double startAngle)` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:43` | `public static HexOrientation Pointy { get; } = new(` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:48` | `public static HexOrientation Flat { get; } = new(` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:62` | `public enum HexDirection` | 已核验 | R04+R10 |
| `Maths/HexagonGrid.cs:83` | `public readonly struct Hex` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:86` | `public int Q { get; }` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:88` | `public int R { get; }` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:90` | `public int S { get; }` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:99` | `public Hex(int q, int r, int s)` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:111` | `public static Hex operator +(Hex a, Hex b)` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:123` | `public static Hex operator -(Hex a, Hex b)` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:135` | `public static Hex operator *(Hex a, int k)` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:147` | `public static Hex operator *(int k, Hex a)` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:156` | `public int Length()` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:166` | `public readonly struct FractionalHex` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:169` | `public double Q { get; }` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:171` | `public double R { get; }` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:173` | `public double S { get; }` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:183` | `public FractionalHex(double q, double r, double s)` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:202` | `public readonly struct HexLayout` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:213` | `public HexOrientation HexOrientation { get; }` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:218` | `public Point Origin { get; }` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:223` | `public Point Size { get; }` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:253` | `public HexLayout(HexOrientation hexOrientation, Point size, Point origin)` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:300` | `public Point HexToPixel(Hex h)` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:311` | `public Hex PixelToHex(Point p)` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:325` | `public Point HexCornerOffset(HexDirection direction)` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:337` | `public List<Point> PolygonCorners(Hex h)` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:363` | `public static class HexExtensions` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:386` | `public static Hex GetRotateLeft(this Hex h)` | 已核验 | R04+R13 |
| `Maths/HexagonGrid.cs:399` | `public static Hex GetRotateRight(this Hex h)` | 已核验 | R04+R13 |
| `Maths/HexagonGrid.cs:414` | `public static Hex GetNeighbor(this Hex h, HexDirection direction)` | 已核验 | R04+R13 |
| `Maths/HexagonGrid.cs:426` | `public static Hex DiagonalNeighbor(this Hex h, HexDirection direction)` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:437` | `public static int Distance(this Hex a, Hex b)` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:446` | `public static Hex HexRound(this FractionalHex h)` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:490` | `public static List<Hex> HexLineDraw(Hex a, Hex b)` | 已核验 | R04 |
| `Maths/HexagonGrid.cs:508` | `public static FractionalHex HexLerp(FractionalHex l, FractionalHex r, double t)` | 已核验 | R04 |
| `Maths/Common.cs:6` | `public struct Point` | 已核验 | R04 |
| `Maths/Common.cs:9` | `public double X { get; }` | 已核验 | R04 |
| `Maths/Common.cs:11` | `public double Y { get; }` | 已核验 | R04 |
| `Maths/Common.cs:16` | `public Point(double x, double y)` | 已核验 | R04 |
| `Maths/Common.cs:26` | `public static class MathUtils` | 已核验 | R04 |
| `Maths/Common.cs:33` | `public static double ToRadians(double degrees)` | 已核验 | R04 |
| `Maths/Common.cs:43` | `public static double ToDegrees(double radians)` | 已核验 | R04 |
| `Maths/Common.cs:53` | `public static float ToRadians(float degrees)` | 已核验 | R04 |
| `Maths/Common.cs:63` | `public static float ToDegrees(float radians)` | 已核验 | R04 |
| `Utility/Disposable.cs:6` | `public abstract class Disposable : IDisposable` | 已核验 | R05 |
| `Utility/Disposable.cs:14` | `public void Dispose()` | 已核验 | R05+R02 |
| `Utility/Disposable.cs:31` | `public class DisposableGroup : Disposable` | 已核验 | R05 |
| `Utility/Disposable.cs:40` | `public void Add(IDisposable? disposable)` | 已核验 | R05+R03 |
| `Utility/Logging.cs:8` | `public enum LogLevel` | 已核验 | R05+R10 |
| `Utility/Logging.cs:27` | `public interface IHandler : IDisposable` | 已核验 | R05 |
| `Utility/Logging.cs:49` | `public interface IFormatter` | 已核验 | R05 |
| `Utility/Logging.cs:62` | `public class LogRecord` | 已核验 | R05 |
| `Utility/Logging.cs:67` | `public DateTime Created { get; }` | 已核验 | R05 |
| `Utility/Logging.cs:72` | `public LogLevel Level { get; }` | 已核验 | R05 |
| `Utility/Logging.cs:77` | `public string LevelName => Level.ToString();` | 已核验 | R05 |
| `Utility/Logging.cs:82` | `public string Message { get; }` | 已核验 | R05 |
| `Utility/Logging.cs:87` | `public string LoggerName { get; }` | 已核验 | R05 |
| `Utility/Logging.cs:92` | `public Exception? Exception { get; }` | 已核验 | R05+R10 |
| `Utility/Logging.cs:102` | `public LogRecord(DateTime created, LogLevel level, string message, string loggerName, Exception? exception = null)` | 已核验 | R05+R10 |
| `Utility/Logging.cs:115` | `public class ConsoleHandler : IHandler` | 已核验 | R05 |
| `Utility/Logging.cs:120` | `public LogLevel Level { get; set; } = LogLevel.NoTest;` | 已核验 | R05 |
| `Utility/Logging.cs:125` | `public IFormatter Formatter { get; set; } = new StandardFormatter();` | 已核验 | R05 |
| `Utility/Logging.cs:130` | `public void Emit(LogRecord record)` | 已核验 | R05 |
| `Utility/Logging.cs:149` | `public void Dispose()` | 已核验 | R05+R02 |
| `Utility/Logging.cs:157` | `public class FileHandler : IHandler` | 已核验 | R05 |
| `Utility/Logging.cs:166` | `public LogLevel Level { get; set; } = LogLevel.NoTest;` | 已核验 | R05 |
| `Utility/Logging.cs:171` | `public IFormatter Formatter { get; set; } = new StandardFormatter();` | 已核验 | R05 |
| `Utility/Logging.cs:178` | `public FileHandler(string filename, bool append = true)` | 已核验 | R05 |
| `Utility/Logging.cs:192` | `public void Emit(LogRecord record)` | 已核验 | R05 |
| `Utility/Logging.cs:204` | `public void Dispose()` | 已核验 | R05+R02 |
| `Utility/Logging.cs:230` | `public class StandardFormatter : IFormatter` | 已核验 | R05 |
| `Utility/Logging.cs:237` | `public string Format(LogRecord record)` | 已核验 | R05 |
| `Utility/Logging.cs:258` | `public class Logger` | 已核验 | R05 |
| `Utility/Logging.cs:274` | `public string Name { get; }` | 已核验 | R05 |
| `Utility/Logging.cs:279` | `public LogLevel Level { get; set; } = LogLevel.NoTest;` | 已核验 | R05 |
| `Utility/Logging.cs:284` | `public static Logger Root => GetLogger("root");` | 已核验 | R05+R13 |
| `Utility/Logging.cs:292` | `public static Logger GetLogger(string name)` | 已核验 | R05+R13 |
| `Utility/Logging.cs:307` | `public void AddHandler(IHandler handler)` | 已核验 | R05 |
| `Utility/Logging.cs:323` | `public void RemoveHandler(IHandler handler)` | 已核验 | R05 |
| `Utility/Logging.cs:341` | `public void ClearHandlers()` | 已核验 | R05+R02 |
| `Utility/Logging.cs:374` | `public void Debug(string message, Exception? exception = null)` | 已核验 | R05+R10 |
| `Utility/Logging.cs:384` | `public void Info(string message, Exception? exception = null)` | 已核验 | R05+R10 |
| `Utility/Logging.cs:394` | `public void Warning(string message, Exception? exception = null)` | 已核验 | R05+R10 |
| `Utility/Logging.cs:404` | `public void Error(string message, Exception? exception = null)` | 已核验 | R05+R10 |
| `Utility/Logging.cs:414` | `public void Critical(string message, Exception? exception = null)` | 已核验 | R05+R10 |
| `Utility/Logging.cs:443` | `public static class Logging` | 已核验 | R05 |
| `Utility/Logging.cs:455` | `public static Logger GetLogger(string name) => Logger.GetLogger(name);` | 已核验 | R05+R13 |
| `Utility/Logging.cs:463` | `public static void BasicConfig(LogLevel level = LogLevel.Info, string filename = "")` | 已核验 | R05 |
| `Utility/Logging.cs:480` | `public static void Debug(string message, Exception? exception = null)` | 已核验 | R05+R10 |
| `Utility/Logging.cs:490` | `public static void Info(string message, Exception? exception = null)` | 已核验 | R05+R10 |
| `Utility/Logging.cs:500` | `public static void Warning(string message, Exception? exception = null)` | 已核验 | R05+R10 |
| `Utility/Logging.cs:510` | `public static void Error(string message, Exception? exception = null)` | 已核验 | R05+R10 |
| `Utility/Logging.cs:520` | `public static void Critical(string message, Exception? exception = null)` | 已核验 | R05+R10 |
| `Utility/FileUtil.cs:8` | `public static class FileUtil` | 已核验 | R05 |
| `Utility/FileUtil.cs:18` | `public static void SaveAsJson<T>(T obj, string filePath)` | 已核验 | R05 |
| `Utility/FileUtil.cs:40` | `public static bool LoadFromJson<T>(string filePath, [System.Diagnostics.CodeAnalysis.MaybeNull] out T obj)` | 已核验 | R05 |
| `Utility/FileUtil.cs:68` | `public static void SaveAsBinary<T>(T obj, string filePath)` | 已核验 | R05 |
| `Utility/FileUtil.cs:90` | `public static bool LoadFromBinary<T>(string filePath, [System.Diagnostics.CodeAnalysis.MaybeNull] out T obj)` | 已核验 | R05 |
| `Utility/SerializeUtil.cs:9` | `public static class SerializeUtil` | 已核验 | R05 |
| `Utility/SerializeUtil.cs:19` | `public static string Serialize<T>(T obj, JsonSerializerOptions? options = null)` | 已核验 | R05+R10 |
| `Utility/SerializeUtil.cs:31` | `public static byte[] SerializeBytes<T>(T obj, JsonSerializerOptions? options = null)` | 已核验 | R05+R10 |
| `Utility/SerializeUtil.cs:45` | `public static T? Deserialize<T>(string bytes, JsonSerializerOptions? options = null)` | 已核验 | R05+R10 |
| `Utility/SerializeUtil.cs:59` | `public static object? Deserialize(string bytes, Type type, JsonSerializerOptions? options = null)` | 已核验 | R05+R10 |
| `Utility/SerializeUtil.cs:73` | `public static T? Deserialize<T>(byte[] bytes, JsonSerializerOptions? options = null)` | 已核验 | R05+R10 |
| `Utility/SerializeUtil.cs:87` | `public static object? Deserialize(byte[] bytes, Type type, JsonSerializerOptions? options = null)` | 已核验 | R05+R10 |
| `Utility/MiscUtil.cs:9` | `public static class MiscUtil` | 已核验 | R05 |
| `Utility/MiscUtil.cs:18` | `public static string GetUniqueTypeName<T>()` | 已核验 | R05+R13 |
| `Utility/MiscUtil.cs:28` | `public static string GetUniqueTypeName(Type type)` | 已核验 | R05+R13 |
| `Utility/MiscUtil.cs:37` | `public static Guid GenerateGuid()` | 已核验 | R05 |
| `Utility/MiscUtil.cs:47` | `public static ulong TypeHash<T>()` | 已核验 | R05 |
| `Utility/MiscUtil.cs:57` | `public static ulong TypeHash(Type type)` | 已核验 | R05 |
| `Utility/MiscUtil.cs:68` | `public static ulong ComputeHash<T>(T data)` | 已核验 | R05 |
| `Utility/TaskUtil.cs:8` | `public static class TaskUtil` | 已核验 | R05+R06 |
| `Utility/TaskUtil.cs:21` | `public static async Task WaitUntil(` | 已核验 | R05+R06 |
| `Utility/TimeUtil.cs:6` | `public static class TimeUtil` | 已核验 | R05 |
| `Utility/TimeUtil.cs:14` | `public static int ToMs(int seconds)` | 已核验 | R05 |
| `Utility/TimeUtil.cs:23` | `public static long GetUtcMilliseconds()` | 已核验 | R05+R13 |
| `Utility/TimeUtil.cs:34` | `public static TimeSpan GetUtcTimeSpanByNow(long milliseconds)` | 已核验 | R05+R13 |
| `Utility/Extensions/EnumeratorExtension.cs:6` | `public static class EnumeratorExtensions` | 已核验 | R05 |
| `Utility/Extensions/EnumeratorExtension.cs:14` | `public static void Apply<T>(this IEnumerable<T> enumerable, Action<T> action)` | 已核验 | R05+R03 |
| `Utility/Extensions/EnumeratorExtension.cs:29` | `public static void Apply<T, TR>(this IEnumerable<T> enumerable, Func<T, TR> func)` | 已核验 | R05+R03 |
| `Utility/Extensions/ListExtension.cs:6` | `public static class ListExtensions` | 已核验 | R05 |
| `Utility/Extensions/ListExtension.cs:15` | `public static void Swap<T>(this IList<T> list, int i, int j)` | 已核验 | R05 |
| `Utility/Extensions/RandomExtension.cs:6` | `public static class RandomExtensions` | 已核验 | R05 |
| `Utility/Extensions/RandomExtension.cs:16` | `public static T Choice<T>(this Random random, IList<T> list)` | 已核验 | R05 |
| `Utility/Extensions/RandomExtension.cs:33` | `public static void Shuffle<T>(this Random random, IList<T> list)` | 已核验 | R05 |
| `Utility/Extensions/RandomExtension.cs:51` | `public static IList<T> Sample<T>(this Random random, IEnumerable<T> sequence, int k)` | 已核验 | R05+R03 |
| `Utility/Extensions/StringExtension.cs:8` | `public static class StringExtensions` | 已核验 | R05 |
| `Utility/Extensions/StringExtension.cs:17` | `public static string Repeat(this string text, int count)` | 已核验 | R05 |
| `Toolkit/ToolkitDefine.cs:8` | `public static class ToolkitLog` | 已核验 | R05 |
| `Toolkit/ToolkitDefine.cs:11` | `public static Logger Logger { get; } = Logging.GetLogger("Toolkit");` | 已核验 | R05+R13 |
| `Toolkit/ToolkitDefine.cs:15` | `public static void Debug(string message)` | 已核验 | R05 |
| `Toolkit/ToolkitDefine.cs:22` | `public static void Info(string message)` | 已核验 | R05 |
| `Toolkit/ToolkitDefine.cs:29` | `public static void Warning(string message)` | 已核验 | R05 |
| `Toolkit/ToolkitDefine.cs:37` | `public static void Error(string message, Exception? exception = null)` | 已核验 | R05+R10 |
| `Toolkit/ConfigTool.cs:10` | `public sealed class IniConfigTool(bool isAutoFlush = false) : Disposable, IUtility` | 已核验 | R05 |
| `Toolkit/ConfigTool.cs:23` | `public void LoadConfig(string filePath)` | 已核验 | R05 |
| `Toolkit/ConfigTool.cs:101` | `public void SaveConfig(string filePath="")` | 已核验 | R05 |
| `Toolkit/ConfigTool.cs:171` | `public T Get<T>(string section, string key, T defaultValue = default!)` | 已核验 | R05+R13 |
| `Toolkit/ConfigTool.cs:238` | `public void Set<T>(string section, string key, T value)` | 已核验 | R05 |
| `Toolkit/ConfigTool.cs:286` | `public void Flush()` | 已核验 | R05 |
| `Toolkit/ConfigTool.cs:306` | `public void Reload()` | 已核验 | R05 |
| `Patterns/StateMachine.cs:6` | `public static class StateEvents` | 已核验 | R14 |
| `Patterns/StateMachine.cs:9` | `public const string EventFinished = "finished";` | 已核验 | R14+R10 |
| `Patterns/StateMachine.cs:14` | `public static readonly State? AnyState = null;` | 已核验 | R14+R10 |
| `Patterns/StateMachine.cs:22` | `public delegate bool StateEventHandler(object? args = null);` | 已核验 | R14 |
| `Patterns/StateMachine.cs:27` | `public class State` | 已核验 | R14 |
| `Patterns/StateMachine.cs:44` | `public string Name => string.IsNullOrEmpty(_name) ? GetType().Name : _name;` | 已核验 | R14+R13 |
| `Patterns/StateMachine.cs:49` | `public StateMachine? StateMachine { get; private set; }` | 已核验 | R14 |
| `Patterns/StateMachine.cs:54` | `public StateMachine? ChildrenStateMachine { get; private set; }` | 已核验 | R14 |
| `Patterns/StateMachine.cs:64` | `public State Named(string name)` | 已核验 | R14 |
| `Patterns/StateMachine.cs:73` | `public virtual void Setup()` | 已核验 | R14 |
| `Patterns/StateMachine.cs:81` | `public virtual void Enter()` | 已核验 | R14 |
| `Patterns/StateMachine.cs:90` | `public virtual void Update(float delta)` | 已核验 | R14 |
| `Patterns/StateMachine.cs:116` | `public virtual void Exit()` | 已核验 | R14 |
| `Patterns/StateMachine.cs:126` | `public void Dispatch(string eventName, object? args = null)` | 已核验 | R14 |
| `Patterns/StateMachine.cs:139` | `public int Depth` | 已核验 | R14 |
| `Patterns/StateMachine.cs:156` | `public void AddState(State subState, bool isInitState=false)` | 已核验 | R14 |
| `Patterns/StateMachine.cs:189` | `public void AddEventHandler(string eventName, StateEventHandler handler)` | 已核验 | R14 |
| `Patterns/StateMachine.cs:211` | `public bool HandleEvent(string eventName, object? args = null)` | 已核验 | R14 |
| `Patterns/StateMachine.cs:225` | `public State CallOnSetup(Action callback)` | 已核验 | R14 |
| `Patterns/StateMachine.cs:234` | `public State CallOnEnter(Action callback)` | 已核验 | R14 |
| `Patterns/StateMachine.cs:243` | `public State CallOnUpdate(Action<float> callback)` | 已核验 | R14 |
| `Patterns/StateMachine.cs:252` | `public State CallOnExit(Action callback)` | 已核验 | R14 |
| `Patterns/StateMachine.cs:319` | `public class Transition(State? fromState, State toState, string eventName)` | 已核验 | R14 |
| `Patterns/StateMachine.cs:321` | `public State? FromState { get; } = fromState;` | 已核验 | R14 |
| `Patterns/StateMachine.cs:322` | `public State ToState { get; } = toState;` | 已核验 | R14 |
| `Patterns/StateMachine.cs:323` | `public string EventName { get; } = eventName;` | 已核验 | R14 |
| `Patterns/StateMachine.cs:329` | `public class StateMachine` | 已核验 | R14 |
| `Patterns/StateMachine.cs:360` | `public StateMachine()` | 已核验 | R14 |
| `Patterns/StateMachine.cs:372` | `public State? CurrentState => _currentState;` | 已核验 | R14 |
| `Patterns/StateMachine.cs:377` | `public bool IsActive => _isActive;` | 已核验 | R14 |
| `Patterns/StateMachine.cs:383` | `public State? InitialState` | 已核验 | R14 |
| `Patterns/StateMachine.cs:400` | `public bool TriggerUpdateWhenStateChange { set; get; } = false;` | 已核验 | R14 |
| `Patterns/StateMachine.cs:409` | `public void AddState(State state)` | 已核验 | R14 |
| `Patterns/StateMachine.cs:450` | `public void AddTransition(State? fromState, State toState, string eventName)` | 已核验 | R14 |
| `Patterns/StateMachine.cs:477` | `public void AddEventHandler(string eventName, StateEventHandler handler)` | 已核验 | R14 |
| `Patterns/StateMachine.cs:502` | `public void SetActive(bool active)` | 已核验 | R14 |
| `Patterns/StateMachine.cs:552` | `public bool Dispatch(string eventName, object? args = null)` | 已核验 | R14 |
| `Patterns/StateMachine.cs:598` | `public void Update(float delta)` | 已核验 | R14 |
| `Patterns/Singleton.cs:6` | `public interface ISingleton` | 已核验 | R14 |
| `Patterns/Singleton.cs:11` | `public void Initialize();` | 已核验 | R14 |
| `Patterns/Singleton.cs:16` | `public void Clear();` | 已核验 | R14+R02 |
| `Patterns/Singleton.cs:22` | `public enum SingletonInitializationStatus` | 已核验 | R14+R10 |
| `Patterns/Singleton.cs:42` | `public abstract class Singleton<T> : ISingleton where T : Singleton<T>, new()` | 已核验 | R14 |
| `Patterns/Singleton.cs:55` | `public static T Instance` | 已核验 | R14 |
| `Patterns/Singleton.cs:78` | `public virtual bool IsInitialized() => _status == SingletonInitializationStatus.Initialized;` | 已核验 | R14+R10 |
| `Patterns/Singleton.cs:83` | `public virtual void Initialize()` | 已核验 | R14 |
| `Patterns/Singleton.cs:118` | `public static void Create()` | 已核验 | R14+R13 |
| `Patterns/Singleton.cs:127` | `public static void Destroy()` | 已核验 | R14 |
| `Patterns/Singleton.cs:138` | `public virtual void Clear()` | 已核验 | R14+R02 |
| `Patterns/ServiceLocator.cs:6` | `public interface IGameService` | 已核验 | R14 |
| `Patterns/ServiceLocator.cs:14` | `public class ServiceLocator : Singleton<ServiceLocator>` | 已核验 | R14 |
| `Patterns/ServiceLocator.cs:24` | `public T Get<T>() where T : IGameService` | 已核验 | R14+R13 |
| `Patterns/ServiceLocator.cs:48` | `public void Register<T>(T service) where T : IGameService` | 已核验 | R14+R13 |
| `Patterns/ServiceLocator.cs:58` | `public void UnRegister<T>() where T : IGameService` | 保留 | R14+R02+R13+R11 |
| `Patterns/ServiceLocator.cs:66` | `public override void Clear()` | 已核验 | R14+R02 |
| `Patterns/PatternDefine.cs:8` | `public class PatternLogger` | 已核验 | R14 |
| `Patterns/PatternDefine.cs:11` | `public static Logger Logger { get; } = Logging.GetLogger("Pattern");` | 已核验 | R14+R13 |
| `Patterns/PatternDefine.cs:15` | `public static void Debug(string message)` | 已核验 | R14 |
| `Patterns/PatternDefine.cs:22` | `public static void Info(string message)` | 已核验 | R14 |
| `Patterns/PatternDefine.cs:29` | `public static void Warning(string message)` | 已核验 | R14 |
| `Patterns/PatternDefine.cs:37` | `public static void Error(string message, Exception? exception = null)` | 已核验 | R14+R10 |
| `Patterns/ObjectPool.cs:7` | `public interface IObjectPool<T> : IDisposable` | 已核验 | R14 |
| `Patterns/ObjectPool.cs:27` | `public interface IPoolCallbackListener` | 已核验 | R14 |
| `Patterns/ObjectPool.cs:45` | `public abstract class AbstractObjectPool<T> : IObjectPool<T>` | 已核验 | R14 |
| `Patterns/ObjectPool.cs:70` | `public T Get()` | 已核验 | R14+R13 |
| `Patterns/ObjectPool.cs:88` | `public void Return(T obj)` | 已核验 | R14 |
| `Patterns/ObjectPool.cs:113` | `public void Clear()` | 已核验 | R14+R02 |
| `Patterns/ObjectPool.cs:127` | `public void Prewarm(int count)` | 已核验 | R14 |
| `Patterns/ObjectPool.cs:140` | `public int Count => Stack.Count;` | 已核验 | R14+R03 |
| `Patterns/ObjectPool.cs:145` | `public void Dispose()` | 已核验 | R14+R02 |
| `Patterns/ObjectPool.cs:191` | `public sealed class ObjectPool<T>(` | 已核验 | R14 |
| `Patterns/MessageChannel.cs:7` | `public interface IPublisher<in T> : IGameService` | 已核验 | R14 |
| `Patterns/MessageChannel.cs:13` | `public void Publish(T message);` | 已核验 | R14 |
| `Patterns/MessageChannel.cs:20` | `public interface ISubscriber<out T> : IGameService` | 已核验 | R14+R02 |
| `Patterns/MessageChannel.cs:27` | `public IDisposable Subscribe(Action<T> handler);` | 已核验 | R14+R02 |
| `Patterns/MessageChannel.cs:34` | `public void Unsubscribe(Action<T> handler);` | 已核验 | R14 |
| `Patterns/MessageChannel.cs:41` | `public interface IMessageChannel<T> : IPublisher<T>, ISubscriber<T>, IDisposable` | 已核验 | R14+R02 |
| `Patterns/MessageChannel.cs:46` | `public bool IsDisposed { get; }` | 已核验 | R14+R02 |
| `Patterns/MessageChannel.cs:53` | `public interface IBufferedMessageChannel<T> : IMessageChannel<T>` | 已核验 | R14 |
| `Patterns/MessageChannel.cs:70` | `public class MessageChannel<T> : IMessageChannel<T>` | 已核验 | R14 |
| `Patterns/MessageChannel.cs:81` | `public void Dispose()` | 已核验 | R14+R02 |
| `Patterns/MessageChannel.cs:90` | `public static EmptySubscription Instance { get; } = new();` | 已核验 | R14 |
| `Patterns/MessageChannel.cs:92` | `public void Dispose()` | 已核验 | R14+R02 |
| `Patterns/MessageChannel.cs:104` | `public bool IsDisposed { get; private set; }` | 已核验 | R14+R02 |
| `Patterns/MessageChannel.cs:107` | `public void Dispose()` | 已核验 | R14+R02 |
| `Patterns/MessageChannel.cs:133` | `public virtual void Publish(T message)` | 已核验 | R14 |
| `Patterns/MessageChannel.cs:194` | `public virtual IDisposable Subscribe(Action<T> handler)` | 已核验 | R14+R02 |
| `Patterns/MessageChannel.cs:223` | `public void Unsubscribe(Action<T> handler)` | 已核验 | R14 |
| `Patterns/MessageChannel.cs:249` | `public class BufferedMessageChannel<T> : MessageChannel<T>, IBufferedMessageChannel<T>` | 已核验 | R14 |
| `Patterns/MessageChannel.cs:252` | `public override void Publish(T message)` | 已核验 | R14 |
| `Patterns/MessageChannel.cs:267` | `public override IDisposable Subscribe(Action<T> handler)` | 已核验 | R14+R02 |
| `Patterns/MessageChannel.cs:288` | `public bool HasBufferedMessage { get; private set; } = false;` | 已核验 | R14 |
| `Patterns/MessageChannel.cs:291` | `public T BufferedMessage { get; private set; } = default!;` | 已核验 | R14 |
| `Patterns/BlackBoard.cs:14` | `public class BlackBoard` | 已核验 | R14 |
| `Patterns/BlackBoard.cs:25` | `public BlackBoard(string name = "BlackBoard", BlackBoard? parent = null)` | 已核验 | R14 |
| `Patterns/BlackBoard.cs:34` | `public string Name { get; }` | 已核验 | R14 |
| `Patterns/BlackBoard.cs:39` | `public BlackBoard? Parent { get; }` | 已核验 | R14 |
| `Patterns/BlackBoard.cs:46` | `public void Register(string key, EventHandler<BlackBoardEventArgs> handler)` | 已核验 | R14+R13 |
| `Patterns/BlackBoard.cs:67` | `public void Unregister(string key, EventHandler<BlackBoardEventArgs> handler)` | 已核验 | R14 |
| `Patterns/BlackBoard.cs:95` | `public void Set<T>(string key, T value)` | 已核验 | R14 |
| `Patterns/BlackBoard.cs:125` | `public T? Get<T>(string key)` | 已核验 | R14+R13 |
| `Patterns/BlackBoard.cs:159` | `public bool TryGet<T>(string key, out T? value)` | 已核验 | R14+R13 |
| `Patterns/BlackBoard.cs:192` | `public bool Contains(string key)` | 已核验 | R14 |
| `Patterns/BlackBoard.cs:210` | `public bool Remove(string key)` | 已核验 | R14+R03 |
| `Patterns/BlackBoard.cs:231` | `public void Clear()` | 已核验 | R14+R02 |
| `Patterns/BlackBoard.cs:289` | `public IReadOnlyList<string> GetKeys()` | 已核验 | R14+R03+R13 |
| `Patterns/BlackBoard.cs:306` | `public IReadOnlyList<object> GetValues()` | 已核验 | R14+R03+R13 |
| `Patterns/BlackBoard.cs:323` | `public IReadOnlyList<KeyValuePair<string, object>> GetEntries()` | 已核验 | R14+R03+R13 |
| `Patterns/BlackBoard.cs:341` | `public enum BlackBoardEventType` | 已核验 | R14+R10 |
| `Patterns/BlackBoard.cs:355` | `public class BlackBoardEventArgs : EventArgs` | 已核验 | R14 |
| `Patterns/BlackBoard.cs:360` | `public string Key { get; }` | 已核验 | R14 |
| `Patterns/BlackBoard.cs:365` | `public BlackBoardEventType EventType { get; }` | 已核验 | R14 |
| `Patterns/BlackBoard.cs:370` | `public object? OldValue { get; }` | 已核验 | R14 |
| `Patterns/BlackBoard.cs:375` | `public object? NewValue { get; }` | 已核验 | R14 |
| `Patterns/BlackBoard.cs:384` | `public BlackBoardEventArgs(string key, BlackBoardEventType type, object? oldValue, object? newValue)` | 已核验 | R14 |
| `ECS/World.cs:8` | `public partial class World : IEnumerable<Archetype>, IEquatable<World>` | 已核验 | R07+R03 |
| `ECS/World.cs:21` | `public World(string name = "World")` | 已核验 | R07 |
| `ECS/World.cs:31` | `public int WorldId { get; }` | 已核验 | R07 |
| `ECS/World.cs:36` | `public string Name { get; init; }` | 已核验 | R07 |
| `ECS/World.cs:41` | `public int EntityCount { get; private set; }` | 已核验 | R07+R03 |
| `ECS/World.cs:51` | `public Entity CreateEntity()` | 已核验 | R07 |
| `ECS/World.cs:63` | `public Entity CreateEntity<T1>(T1 c1) where T1 : IComponent` | 已核验 | R07 |
| `ECS/World.cs:78` | `public Entity CreateEntity<T1, T2>(T1 c1, T2 c2)` | 已核验 | R07 |
| `ECS/World.cs:98` | `public Entity CreateEntity<T1, T2, T3>(T1 c1, T2 c2, T3 c3)` | 已核验 | R07 |
| `ECS/World.cs:122` | `public Entity CreateEntity<T1, T2, T3, T4>(T1 c1, T2 c2, T3 c3, T4 c4)` | 已核验 | R07 |
| `ECS/World.cs:140` | `public bool DestroyEntity(Entity entity)` | 已核验 | R07 |
| `ECS/World.cs:167` | `public bool IsAlive(Entity entity)` | 已核验 | R07 |
| `ECS/World.cs:178` | `public bool Has<T>(Entity entity) where T : IComponent` | 已核验 | R07 |
| `ECS/World.cs:189` | `public ref T Get<T>(Entity entity) where T : IComponent` | 已核验 | R07+R13 |
| `ECS/World.cs:207` | `public bool TryGet<T>(Entity entity, out T component) where T : IComponent` | 已核验 | R07+R13 |
| `ECS/World.cs:228` | `public Entity Add<T>(Entity entity, T component) where T : IComponent` | 已核验 | R07 |
| `ECS/World.cs:254` | `public Entity Set<T>(Entity entity, T component) where T : IComponent` | 已核验 | R07 |
| `ECS/World.cs:273` | `public bool Remove<T>(Entity entity) where T : IComponent` | 已核验 | R07 |
| `ECS/World.cs:296` | `public Archetype GetArchetype(Entity entity)` | 已核验 | R07+R13 |
| `ECS/World.cs:306` | `public Entity? GetEntity(int id)` | 已核验 | R07+R13 |
| `ECS/World.cs:321` | `public IEnumerable<Entity> GetEntities()` | 已核验 | R07+R03+R13 |
| `ECS/World.cs:337` | `public Query Query()` | 已核验 | R07 |
| `ECS/World.cs:347` | `public Query Query<T1>() where T1 : IComponent` | 已核验 | R07 |
| `ECS/World.cs:358` | `public Query Query<T1, T2>()` | 已核验 | R07 |
| `ECS/World.cs:372` | `public Query Query<T1, T2, T3>()` | 已核验 | R07 |
| `ECS/World.cs:388` | `public Query Query<T1, T2, T3, T4>()` | 已核验 | R07 |
| `ECS/World.cs:402` | `public Entity Instantiate(EntityPrefab prefab)` | 已核验 | R07 |
| `ECS/World.cs:411` | `public void Destroy()` | 已核验 | R07 |
| `ECS/World.cs:443` | `public override string ToString()` | 已核验 | R07 |
| `ECS/World.cs:448` | `public IEnumerator<Archetype> GetEnumerator()` | 已核验 | R07+R13 |
| `ECS/World.cs:458` | `public bool Equals(World? other)` | 已核验 | R07 |
| `ECS/World.cs:567` | `public int EntityId;` | 已核验 | R07 |
| `ECS/World.cs:568` | `public int Version;` | 已核验 | R07 |
| `ECS/World.cs:569` | `public bool Alive;` | 已核验 | R07 |
| `ECS/World.cs:570` | `public Archetype Archetype = null!;` | 已核验 | R07 |
| `ECS/World.cs:571` | `public int Row;` | 已核验 | R07 |
| `ECS/TypeSignature.cs:9` | `public sealed class TypeSignature : IEquatable<TypeSignature>, IReadOnlyCollection<Type>` | 已核验 | R07+R03 |
| `ECS/TypeSignature.cs:21` | `public TypeSignature(params Type[] types)` | 已核验 | R07 |
| `ECS/TypeSignature.cs:30` | `public TypeSignature(IEnumerable<Type> types)` | 已核验 | R07+R03 |
| `ECS/TypeSignature.cs:42` | `public TypeSignature(TypeSignature signature)` | 已核验 | R07 |
| `ECS/TypeSignature.cs:53` | `public int Count => _types.Length;` | 已核验 | R07+R03 |
| `ECS/TypeSignature.cs:60` | `public bool Has<T>() where T : IComponent` | 已核验 | R07 |
| `ECS/TypeSignature.cs:70` | `public bool Has(Type type)` | 已核验 | R07 |
| `ECS/TypeSignature.cs:80` | `public bool HasAll(TypeSignature signature)` | 已核验 | R07 |
| `ECS/TypeSignature.cs:91` | `public bool HasAny(TypeSignature signature)` | 已核验 | R07 |
| `ECS/TypeSignature.cs:97` | `public IEnumerator<Type> GetEnumerator()` | 已核验 | R07+R13 |
| `ECS/TypeSignature.cs:107` | `public bool Equals(TypeSignature? other)` | 已核验 | R07 |
| `ECS/TypeSignature.cs:117` | `public override bool Equals(object? obj)` | 已核验 | R07 |
| `ECS/TypeSignature.cs:122` | `public override int GetHashCode()` | 已核验 | R07+R13 |
| `ECS/TypeSignature.cs:127` | `public override string ToString()` | 已核验 | R07 |
| `ECS/TypeSignature.cs:197` | `public long Value { get; } = value;` | 已核验 | R07 |
| `ECS/SystemGroup.cs:6` | `public sealed class SystemGroup` | 已核验 | R07 |
| `ECS/SystemGroup.cs:17` | `public void Add(EcsSystem system)` | 已核验 | R07+R03 |
| `ECS/SystemGroup.cs:27` | `public void Add(EcsSystem system, int order)` | 已核验 | R07+R03 |
| `ECS/SystemGroup.cs:46` | `public bool Remove(EcsSystem system)` | 已核验 | R07+R03 |
| `ECS/SystemGroup.cs:65` | `public void Validate()` | 已核验 | R07 |
| `ECS/SystemGroup.cs:74` | `public void Update()` | 已核验 | R07 |
| `ECS/SystemGroup.cs:84` | `public void Update(float deltaTime)` | 已核验 | R07 |
| `ECS/SystemDependencyAttributes.cs:7` | `public sealed class RunBeforeAttribute : Attribute` | 已核验 | R07+R10 |
| `ECS/SystemDependencyAttributes.cs:13` | `public RunBeforeAttribute(Type systemType)` | 已核验 | R07+R10 |
| `ECS/SystemDependencyAttributes.cs:21` | `public Type SystemType { get; }` | 已核验 | R07 |
| `ECS/SystemDependencyAttributes.cs:39` | `public sealed class RunAfterAttribute : Attribute` | 已核验 | R07+R10 |
| `ECS/SystemDependencyAttributes.cs:45` | `public RunAfterAttribute(Type systemType)` | 已核验 | R07+R10 |
| `ECS/SystemDependencyAttributes.cs:53` | `public Type SystemType { get; }` | 已核验 | R07 |
| `ECS/System.cs:6` | `public abstract class EcsSystem` | 已核验 | R07 |
| `ECS/System.cs:26` | `public bool Enabled { get; set; } = true;` | 已核验 | R07 |
| `ECS/System.cs:31` | `public abstract void Update();` | 已核验 | R07 |
| `ECS/System.cs:37` | `public virtual void Update(float deltaTime)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:6` | `public readonly struct BufferedEntity : IEquatable<BufferedEntity>` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:24` | `public int Id { get; }` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:31` | `public bool Equals(BufferedEntity other)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:37` | `public override bool Equals(object? obj)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:43` | `public override int GetHashCode()` | 已核验 | R07+R13 |
| `ECS/CommandBuffer.cs:48` | `public static bool operator ==(BufferedEntity left, BufferedEntity right)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:53` | `public static bool operator !=(BufferedEntity left, BufferedEntity right)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:62` | `public sealed class CommandBufferResult` | 已核验 | R07+R10 |
| `ECS/CommandBuffer.cs:77` | `public Entity Resolve(BufferedEntity entity)` | 已核验 | R07+R13 |
| `ECS/CommandBuffer.cs:93` | `public bool TryResolve(BufferedEntity entity, out Entity resolved)` | 已核验 | R07+R13 |
| `ECS/CommandBuffer.cs:102` | `public sealed class CommandBuffer` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:116` | `public CommandBuffer(World world)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:126` | `public BufferedEntity CreateEntity()` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:138` | `public BufferedEntity CreateEntity<T1>(T1 c1) where T1 : IComponent` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:153` | `public BufferedEntity CreateEntity<T1, T2>(T1 c1, T2 c2)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:166` | `public void DestroyEntity(Entity entity)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:176` | `public void DestroyEntity(BufferedEntity entity)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:189` | `public void Add<T>(Entity entity, T component) where T : IComponent` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:203` | `public void Add<T>(BufferedEntity entity, T component) where T : IComponent` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:217` | `public void Set<T>(Entity entity, T component) where T : IComponent` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:231` | `public void Set<T>(BufferedEntity entity, T component) where T : IComponent` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:243` | `public void Remove<T>(Entity entity) where T : IComponent` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:254` | `public void Remove<T>(BufferedEntity entity) where T : IComponent` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:266` | `public CommandBufferResult Playback()` | 已核验 | R07+R10 |
| `ECS/CommandBuffer.cs:322` | `public EntityTarget(Entity entity)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:329` | `public EntityTarget(BufferedEntity entity)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:336` | `public Entity Resolve(PlaybackContext context)` | 已核验 | R07+R13 |
| `ECS/CommandBuffer.cs:346` | `public PlaybackContext(World world)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:351` | `public World World { get; }` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:353` | `public IReadOnlyDictionary<BufferedEntity, Entity> CreatedEntities => _createdEntities;` | 已核验 | R07+R03 |
| `ECS/CommandBuffer.cs:355` | `public void AddCreatedEntity(BufferedEntity bufferedEntity, Entity entity)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:360` | `public Entity Resolve(BufferedEntity bufferedEntity)` | 已核验 | R07+R13 |
| `ECS/CommandBuffer.cs:376` | `public CreateEntityCommand(BufferedEntity bufferedEntity, IComponent[] components)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:382` | `public void Playback(PlaybackContext context)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:393` | `public DestroyEntityCommand(EntityTarget target)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:398` | `public void Playback(PlaybackContext context)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:409` | `public AddCommand(EntityTarget target, T component)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:415` | `public void Playback(PlaybackContext context)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:426` | `public SetCommand(EntityTarget target, T component)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:432` | `public void Playback(PlaybackContext context)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:442` | `public RemoveCommand(EntityTarget target)` | 已核验 | R07 |
| `ECS/CommandBuffer.cs:447` | `public void Playback(PlaybackContext context)` | 已核验 | R07 |
| `ECS/ComponentColumn.cs:25` | `public Type ComponentType => typeof(T);` | 已核验 | R07 |
| `ECS/ComponentColumn.cs:27` | `public int Count => _items.Count;` | 已核验 | R07+R03 |
| `ECS/ComponentColumn.cs:29` | `public void Add(T component)` | 已核验 | R07+R03 |
| `ECS/ComponentColumn.cs:34` | `public void AddBoxed(IComponent component)` | 已核验 | R07 |
| `ECS/ComponentColumn.cs:39` | `public void SetBoxed(int row, IComponent component)` | 已核验 | R07 |
| `ECS/ComponentColumn.cs:44` | `public IComponent GetBoxed(int row)` | 已核验 | R07+R13 |
| `ECS/ComponentColumn.cs:49` | `public ref T GetRef(int row)` | 已核验 | R07+R13 |
| `ECS/ComponentColumn.cs:54` | `public void RemoveAtSwapBack(int row)` | 已核验 | R07 |
| `ECS/ECSExtension.cs:9` | `public delegate void EachRef<T1>(Entity entity, ref T1 c1) where T1 : IComponent;` | 已核验 | R07 |
| `ECS/ECSExtension.cs:19` | `public delegate void EachRef<T1, T2>(Entity entity, ref T1 c1, ref T2 c2)` | 已核验 | R07 |
| `ECS/ECSExtension.cs:26` | `public static class ECSExtension` | 已核验 | R07 |
| `ECS/ECSExtension.cs:34` | `public static void Each<T1>(this World world, EachRef<T1> action) where T1 : IComponent` | 已核验 | R07 |
| `ECS/ECSExtension.cs:52` | `public static void Each<T1, T2>(this World world, EachRef<T1, T2> action)` | 已核验 | R07 |
| `ECS/EntityPrefab.cs:6` | `public sealed class EntityPrefab` | 已核验 | R07 |
| `ECS/EntityPrefab.cs:18` | `public static EntityPrefab Create()` | 已核验 | R07+R13 |
| `ECS/EntityPrefab.cs:31` | `public EntityPrefab With<T>(T component) where T : IComponent` | 已核验 | R07 |
| `ECS/Query.cs:8` | `public sealed class Query : IEnumerable<Entity>` | 已核验 | R07+R03 |
| `ECS/Query.cs:21` | `public Query(World world)` | 已核验 | R07 |
| `ECS/Query.cs:30` | `public World World => _world;` | 已核验 | R07 |
| `ECS/Query.cs:36` | `public Query Has<T>() where T : IComponent` | 已核验 | R07 |
| `ECS/Query.cs:45` | `public Query Has(Type type)` | 已核验 | R07 |
| `ECS/Query.cs:56` | `public Query Not<T>() where T : IComponent` | 已核验 | R07 |
| `ECS/Query.cs:65` | `public Query Not(Type type)` | 已核验 | R07 |
| `ECS/Query.cs:75` | `public Query Clear()` | 已核验 | R07+R02 |
| `ECS/Query.cs:87` | `public IReadOnlyList<Archetype> GetArchetypes()` | 已核验 | R07+R03+R13 |
| `ECS/Query.cs:97` | `public void Foreach(Action<Entity> action)` | 已核验 | R07 |
| `ECS/Query.cs:107` | `public IEnumerator<Entity> GetEnumerator()` | 已核验 | R07+R13 |
| `ECS/Query.cs:128` | `public override string ToString()` | 已核验 | R07 |
| `ECS/Entity.cs:6` | `public readonly struct Entity : IEquatable<Entity>` | 已核验 | R07 |
| `ECS/Entity.cs:8` | `public Entity(int worldId, int id, int version)` | 已核验 | R07 |
| `ECS/Entity.cs:18` | `public int WorldId { get; }` | 已核验 | R07 |
| `ECS/Entity.cs:23` | `public int Id { get; }` | 已核验 | R07 |
| `ECS/Entity.cs:28` | `public int Version { get; }` | 已核验 | R07 |
| `ECS/Entity.cs:30` | `public bool Equals(Entity other)` | 已核验 | R07 |
| `ECS/Entity.cs:35` | `public override bool Equals(object? obj)` | 已核验 | R07 |
| `ECS/Entity.cs:40` | `public override int GetHashCode()` | 已核验 | R07+R13 |
| `ECS/Entity.cs:45` | `public override string ToString()` | 已核验 | R07 |
| `ECS/Entity.cs:50` | `public static bool operator ==(Entity left, Entity right)` | 已核验 | R07 |
| `ECS/Entity.cs:55` | `public static bool operator !=(Entity left, Entity right)` | 已核验 | R07 |
| `ECS/Archetype.cs:8` | `public sealed class Archetype : IEnumerable<Entity>` | 已核验 | R07+R03 |
| `ECS/Archetype.cs:26` | `public TypeSignature Signature { get; }` | 已核验 | R07 |
| `ECS/Archetype.cs:31` | `public int EntityCount => _entities.Count;` | 已核验 | R07+R03 |
| `ECS/Archetype.cs:122` | `public bool Has<T>() where T : IComponent` | 已核验 | R07 |
| `ECS/Archetype.cs:132` | `public bool Has(Type type)` | 已核验 | R07 |
| `ECS/Archetype.cs:142` | `public bool HasAll(TypeSignature signature)` | 已核验 | R07 |
| `ECS/Archetype.cs:152` | `public bool HasAny(TypeSignature signature)` | 已核验 | R07 |
| `ECS/Archetype.cs:157` | `public IEnumerator<Entity> GetEnumerator()` | 已核验 | R07+R13 |
| `ECS/Archetype.cs:167` | `public override string ToString()` | 已核验 | R07 |
| `ECS/Component.cs:6` | `public interface IComponent` | 已核验 | R07 |
| `GDExt/Utils.cs:11` | `public class NodeObjectPool : Singleton<NodeObjectPool>` | 已核验 | R15 |
| `GDExt/Utils.cs:20` | `public Node2D Get(PackedScene scene)` | 已核验 | R15+R13 |
| `GDExt/Utils.cs:34` | `public override void Clear()` | 已核验 | R15+R02 |
| `GDExt/Utils.cs:49` | `public void Return(PackedScene scene, Node2D node)` | 已核验 | R15 |
| `GDExt/Extensions.cs:8` | `public static class UnRegisterAbleExtensions` | 保留 | R15+R02+R13+R11 |
| `GDExt/Extensions.cs:16` | `public static IUnRegister UnRegisterWhenNodeExit(this IUnRegister self, Godot.Node node)` | 保留 | R15+R02+R13+R11 |
| `GDExt/Extensions.cs:27` | `public static class GdConst` | 已核验 | R15 |
| `GDExt/Extensions.cs:32` | `public const long ServerId = 1;` | 已核验 | R15+R10 |
| `GDExt/Extensions.cs:37` | `public const int Timeout = 5000;` | 已核验 | R15+R10 |
| `GDExt/Extensions.cs:43` | `public static class Channel` | 已核验 | R15 |
| `GDExt/Extensions.cs:48` | `public const int Gameplay = 1;` | 已核验 | R15+R10 |
| `GDExt/Extensions.cs:53` | `public const int System = 2;` | 已核验 | R15+R10 |
| `GDExt/Extensions.cs:58` | `public const int Data = 3;` | 已核验 | R15+R10 |
| `Net/Core/GameNetOptions.cs:6` | `public sealed class NetApplicationInfo` | 已核验 | R08 |
| `Net/Core/GameNetOptions.cs:11` | `public required Guid ApplicationId { get; init; }` | 已核验 | R08 |
| `Net/Core/GameNetOptions.cs:16` | `public int ProtocolVersion { get; init; } = 1;` | 已核验 | R08 |
| `Net/Core/GameNetOptions.cs:22` | `public sealed class GameNetOptions` | 已核验 | R08+R10 |
| `Net/Core/GameNetOptions.cs:27` | `public string? DebugName { get; init; }` | 已核验 | R08 |
| `Net/Core/GameNetOptions.cs:32` | `public required NetApplicationInfo Application { get; init; }` | 已核验 | R08 |
| `Net/Core/GameNetOptions.cs:37` | `public TimeProvider TimeProvider { get; init; } = TimeProvider.System;` | 已核验 | R08 |
| `Net/Core/GameNetOptions.cs:42` | `public INetEventDispatcher? EventDispatcher { get; init; }` | 已核验 | R08 |
| `Net/Core/GameNetOptions.cs:47` | `public DiscoveryOptions Discovery { get; init; } = new();` | 已核验 | R08+R10 |
| `Net/Core/GameNetOptions.cs:52` | `public NetFingerprintPolicy MessageFingerprintPolicy { get; init; } = NetFingerprintPolicy.Strict;` | 已核验 | R08 |
| `Net/Core/GameNetOptions.cs:57` | `public int MaxPacketSize { get; init; } = 64 * 1024;` | 已核验 | R08 |
| `Net/Core/GameNetOptions.cs:62` | `public int MaxSendQueueBytesPerPeer { get; init; } = 1024 * 1024;` | 已核验 | R08 |
| `Net/Core/GameNetOptions.cs:67` | `public int MaxSendQueuePacketsPerPeer { get; init; } = 1024;` | 已核验 | R08 |
| `Net/Core/GameNetOptions.cs:72` | `public int MaxSendsPerSecondPerPeer { get; init; } = int.MaxValue;` | 已核验 | R08 |
| `Net/Core/GameNet.cs:9` | `public sealed class GameNet : IAsyncDisposable` | 已核验 | R08+R06 |
| `Net/Core/GameNet.cs:48` | `public GameNet(GameNetOptions options, IDiscoveryBackend? discoveryBackend = null)` | 已核验 | R08+R10 |
| `Net/Core/GameNet.cs:77` | `public GameNet(INetTransport transport, GameNetOptions options, IDiscoveryBackend? discoveryBackend = null)` | 已核验 | R08+R10 |
| `Net/Core/GameNet.cs:114` | `public GameNetOptions Options => _options;` | 已核验 | R08+R10 |
| `Net/Core/GameNet.cs:119` | `public NetDiagnostics Diagnostics { get; }` | 已核验 | R08 |
| `Net/Core/GameNet.cs:124` | `public NetSession Session { get; }` | 已核验 | R08 |
| `Net/Core/GameNet.cs:129` | `public PeerDirectory Peers { get; }` | 已核验 | R08 |
| `Net/Core/GameNet.cs:134` | `public NetDiscovery Discovery { get; }` | 已核验 | R08 |
| `Net/Core/GameNet.cs:139` | `public NetMessenger Messages { get; }` | 已核验 | R08 |
| `Net/Core/GameNet.cs:144` | `public NetStats Stats { get; }` | 已核验 | R08 |
| `Net/Core/GameNet.cs:149` | `public NetFlow Flow { get; }` | 已核验 | R08 |
| `Net/Core/GameNet.cs:156` | `public void On<T>(Action<NetContext, T> handler) => Messages.On(handler);` | 已核验 | R08 |
| `Net/Core/GameNet.cs:161` | `public void On<T>(Func<NetContext, T, Task> handler) => Messages.On(handler);` | 已核验 | R08+R06 |
| `Net/Core/GameNet.cs:169` | `public void OnRequest<TRequest, TResponse>(Func<NetContext, TRequest, TResponse> handler) =>` | 已核验 | R08 |
| `Net/Core/GameNet.cs:175` | `public void OnRequest<TRequest, TResponse>(Func<NetContext, TRequest, Task<TResponse>> handler) =>` | 已核验 | R08+R06 |
| `Net/Core/GameNet.cs:183` | `public ValueTask<NetSendResult> SendToServerAsync<T>(T message)` | 已核验 | R08+R06+R10 |
| `Net/Core/GameNet.cs:196` | `public ValueTask<NetSendResult> SendAsync<T>(PeerId peerId, T message)` | 已核验 | R08+R06+R10 |
| `Net/Core/GameNet.cs:208` | `public ValueTask<NetSendResult> BroadcastAsync<T>(T message)` | 已核验 | R08+R06+R10 |
| `Net/Core/GameNet.cs:224` | `public Task<NetRequestResult<TResponse>> RequestAsync<TRequest, TResponse>(` | 已核验 | R08+R06+R10 |
| `Net/Core/GameNet.cs:243` | `public Task<NetSendResult> RelayAsync<T>(` | 已核验 | R08+R06+R10 |
| `Net/Core/GameNet.cs:259` | `public Task<NetSessionResult> HostAsync(HostOptions options, CancellationToken token = default)` | 已核验 | R08+R06+R10 |
| `Net/Core/GameNet.cs:269` | `public Task<NetSessionResult> StartServerAsync(HostOptions options, CancellationToken token = default)` | 已核验 | R08+R06+R10 |
| `Net/Core/GameNet.cs:280` | `public async Task<JoinResult> JoinAsync(JoinOptions options, CancellationToken token = default)` | 已核验 | R08+R06+R10 |
| `Net/Core/GameNet.cs:497` | `public async Task<NetSessionResult> LeaveAsync(CancellationToken token = default)` | 已核验 | R08+R06+R10 |
| `Net/Core/GameNet.cs:508` | `public async Task<NetSessionResult> KickAsync(PeerId peerId, DisconnectReason reason = DisconnectReason.Kicked)` | 已核验 | R08+R06+R10 |
| `Net/Core/GameNet.cs:540` | `public async Task<NetSessionResult> StopAsync(CancellationToken token = default)` | 已核验 | R08+R06+R10 |
| `Net/Core/GameNet.cs:619` | `public ValueTask DisposeAsync()` | 已核验 | R08+R06+R02 |
| `Net/Core/GameNet.cs:694` | `public static void ValidateOptions(GameNetOptions options)` | 已核验 | R08+R10 |
| `Net/Core/INetEventDispatcher.cs:6` | `public interface INetEventDispatcher` | 已核验 | R08 |
| `Net/Core/NetChannel.cs:6` | `public enum NetChannel` | 已核验 | R08+R10 |
| `Net/Core/NetResults.cs:6` | `public enum NetSendStatus` | 已核验 | R08+R10 |
| `Net/Core/NetResults.cs:26` | `public readonly record struct NetSendResult(NetSendStatus Status, string? Message = null)` | 已核验 | R08+R10 |
| `Net/Core/NetResults.cs:31` | `public bool Succeeded => Status == NetSendStatus.Ok;` | 已核验 | R08+R10 |
| `Net/Core/NetResults.cs:36` | `public static NetSendResult Ok() => new(NetSendStatus.Ok);` | 已核验 | R08+R10 |
| `Net/Core/NetResults.cs:42` | `public enum NetTransportStatus` | 已核验 | R08+R10 |
| `Net/Core/NetResults.cs:57` | `public enum NetSessionStatus` | 已核验 | R08+R10 |
| `Net/Core/NetResults.cs:75` | `public readonly record struct NetSessionResult(NetSessionStatus Status, string? Message = null)` | 已核验 | R08+R10 |
| `Net/Core/NetResults.cs:80` | `public bool Succeeded => Status == NetSessionStatus.Ok;` | 已核验 | R08+R10 |
| `Net/Core/NetResults.cs:85` | `public static NetSessionResult Ok() => new(NetSessionStatus.Ok);` | 已核验 | R08+R10 |
| `Net/Core/NetResults.cs:91` | `public enum NetRequestStatus` | 已核验 | R08+R10 |
| `Net/Core/NetResults.cs:110` | `public sealed class NetRequestResult<TResponse>` | 已核验 | R08+R10 |
| `Net/Core/NetResults.cs:115` | `public required NetRequestStatus Status { get; init; }` | 已核验 | R08+R10 |
| `Net/Core/NetResults.cs:120` | `public TResponse? Response { get; init; }` | 已核验 | R08 |
| `Net/Core/NetResults.cs:125` | `public string? Message { get; init; }` | 已核验 | R08 |
| `Net/Core/NetResults.cs:131` | `public enum FlowEndReason` | 已核验 | R08+R10 |
| `Net/Core/NetIds.cs:6` | `public readonly record struct PeerId(ulong Value)` | 已核验 | R08+R10 |
| `Net/Core/NetIds.cs:11` | `public static readonly PeerId None = new(0);` | 已核验 | R08+R10 |
| `Net/Core/NetIds.cs:16` | `public static readonly PeerId Server = new(1);` | 已核验 | R08+R10 |
| `Net/Core/NetIds.cs:22` | `public readonly record struct TransportConnectionId(ulong Value)` | 已核验 | R08+R10 |
| `Net/Core/NetIds.cs:27` | `public static readonly TransportConnectionId None = new(0);` | 已核验 | R08+R10 |
| `Net/Core/NetIds.cs:33` | `public readonly record struct RoomId(string Value)` | 已核验 | R08+R10 |
| `Net/Core/NetIds.cs:38` | `public static readonly RoomId None = new(string.Empty);` | 已核验 | R08+R10 |
| `Net/Diagnostics/NetDiagnostics.cs:6` | `public sealed class NetDiagnostics` | 已核验 | R08 |
| `Net/Diagnostics/NetDiagnostics.cs:22` | `public event Action<NetError>? ErrorRecorded;` | 已核验 | R08+R02 |
| `Net/Diagnostics/NetDiagnostics.cs:28` | `public NetDiagnostics(INetEventDispatcher? dispatcher = null)` | 已核验 | R08 |
| `Net/Diagnostics/NetDiagnostics.cs:36` | `public void AddPacketSent(long bytes)` | 已核验 | R08 |
| `Net/Diagnostics/NetDiagnostics.cs:45` | `public void AddPacketReceived(long bytes)` | 已核验 | R08 |
| `Net/Diagnostics/NetDiagnostics.cs:54` | `public void AddDroppedPacket() => Interlocked.Increment(ref _droppedPackets);` | 已核验 | R08 |
| `Net/Diagnostics/NetDiagnostics.cs:59` | `public void AddError() => RecordError(NetError.Unspecified);` | 已核验 | R08 |
| `Net/Diagnostics/NetDiagnostics.cs:64` | `public void RecordError(NetError error)` | 已核验 | R08 |
| `Net/Diagnostics/NetDiagnostics.cs:79` | `public NetDiagnosticsSnapshot GetSnapshot() => new()` | 已核验 | R08+R13 |
| `Net/Diagnostics/NetDiagnostics.cs:136` | `public sealed class NetDiagnosticsSnapshot` | 已核验 | R08 |
| `Net/Diagnostics/NetDiagnostics.cs:141` | `public int ConnectedPeerCount { get; init; }` | 已核验 | R08+R03 |
| `Net/Diagnostics/NetDiagnostics.cs:146` | `public int PendingRequestCount { get; init; }` | 已核验 | R08+R03 |
| `Net/Diagnostics/NetDiagnostics.cs:151` | `public int PendingFlowCount { get; init; }` | 已核验 | R08+R03 |
| `Net/Diagnostics/NetDiagnostics.cs:156` | `public long PacketsSent { get; init; }` | 已核验 | R08 |
| `Net/Diagnostics/NetDiagnostics.cs:161` | `public long PacketsReceived { get; init; }` | 已核验 | R08 |
| `Net/Diagnostics/NetDiagnostics.cs:166` | `public long BytesSent { get; init; }` | 已核验 | R08 |
| `Net/Diagnostics/NetDiagnostics.cs:171` | `public long BytesReceived { get; init; }` | 已核验 | R08 |
| `Net/Diagnostics/NetDiagnostics.cs:176` | `public long DroppedPackets { get; init; }` | 已核验 | R08 |
| `Net/Diagnostics/NetDiagnostics.cs:181` | `public long ErrorCount { get; init; }` | 已核验 | R08+R03 |
| `Net/Diagnostics/NetDiagnostics.cs:187` | `public sealed record NetError(string Code, string Message, Exception? Exception = null)` | 已核验 | R08+R10 |
| `Net/Diagnostics/NetDiagnostics.cs:192` | `public static readonly NetError Unspecified = new("Unspecified", "An unspecified networking error occurred.");` | 已核验 | R08+R10 |
| `Net/Discovery/NetDiscovery.cs:13` | `public sealed class DiscoveryOptions` | 已核验 | R09+R10 |
| `Net/Discovery/NetDiscovery.cs:18` | `public int Port { get; init; } = 3344;` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:23` | `public TimeSpan AdvertiseInterval { get; init; } = TimeSpan.FromSeconds(1);` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:28` | `public int MaxMetadataPayloadSize { get; init; } = 8 * 1024;` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:33` | `public TimeSpan RoomTimeout { get; init; } = TimeSpan.FromSeconds(5);` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:38` | `public TimeSpan DefaultScanTimeout { get; init; } = TimeSpan.FromMilliseconds(200);` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:44` | `public readonly record struct DiscoveryAdvertisementId(Guid Value)` | 已核验 | R09+R10 |
| `Net/Discovery/NetDiscovery.cs:49` | `public static readonly DiscoveryAdvertisementId None = new(Guid.Empty);` | 已核验 | R09+R10 |
| `Net/Discovery/NetDiscovery.cs:55` | `public interface IDiscoveryBackend : IAsyncDisposable` | 已核验 | R09+R06 |
| `Net/Discovery/NetDiscovery.cs:90` | `public sealed class DiscoveryMetadataAttribute : Attribute` | 已核验 | R09+R10 |
| `Net/Discovery/NetDiscovery.cs:97` | `public DiscoveryMetadataAttribute(string schemaKey)` | 已核验 | R09+R10 |
| `Net/Discovery/NetDiscovery.cs:109` | `public string SchemaKey { get; }` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:114` | `public uint SchemaId { get; }` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:120` | `public sealed class LanAdvertiseInfo` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:125` | `public required string RoomId { get; init; }` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:130` | `public required int GamePort { get; init; }` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:135` | `public required uint MetadataSchemaId { get; init; }` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:142` | `public sealed class LanScanResult<TMetadata>` | 已核验 | R09+R10 |
| `Net/Discovery/NetDiscovery.cs:147` | `public required string RoomId { get; init; }` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:152` | `public required int GamePort { get; init; }` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:157` | `public IPEndPoint? EndPoint { get; init; }` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:162` | `public required bool IsJoinable { get; init; }` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:167` | `public required Guid ApplicationId { get; init; }` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:172` | `public required int ProtocolVersion { get; init; }` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:177` | `public required uint MetadataSchemaId { get; init; }` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:182` | `public required TMetadata Metadata { get; init; }` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:187` | `public TimeSpan? EstimatedLatency { get; init; }` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:194` | `public sealed class LanBrowserSnapshot<TMetadata>` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:199` | `public required IReadOnlyList<LanScanResult<TMetadata>> Rooms { get; init; }` | 已核验 | R09+R03+R10 |
| `Net/Discovery/NetDiscovery.cs:206` | `public sealed class LanBrowser<TMetadata> : IAsyncDisposable` | 已核验 | R09+R06 |
| `Net/Discovery/NetDiscovery.cs:247` | `public event Action<LanScanResult<TMetadata>>? RoomFound;` | 已核验 | R09+R02+R10 |
| `Net/Discovery/NetDiscovery.cs:252` | `public event Action<LanScanResult<TMetadata>>? RoomUpdated;` | 已核验 | R09+R02+R10 |
| `Net/Discovery/NetDiscovery.cs:257` | `public event Action<LanScanResult<TMetadata>>? RoomLost;` | 已核验 | R09+R02+R10 |
| `Net/Discovery/NetDiscovery.cs:262` | `public LanBrowserSnapshot<TMetadata> Snapshot { get; private set; }` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:267` | `public async Task RefreshAsync()` | 已核验 | R09+R06 |
| `Net/Discovery/NetDiscovery.cs:320` | `public ValueTask DisposeAsync()` | 已核验 | R09+R06+R02 |
| `Net/Discovery/NetDiscovery.cs:404` | `public sealed class DiscoveryMetadataRegistry` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:413` | `public DiscoveryMetadataDescriptor Register<TMetadata>(string schemaKey)` | 已核验 | R09+R13 |
| `Net/Discovery/NetDiscovery.cs:435` | `public static uint GetSchemaId(string schemaKey)` | 已核验 | R09+R13 |
| `Net/Discovery/NetDiscovery.cs:459` | `public sealed record DiscoveryMetadataDescriptor(Type MetadataType, string SchemaKey, uint SchemaId);` | 已核验 | R09+R10 |
| `Net/Discovery/NetDiscovery.cs:464` | `public sealed class MemoryDiscoveryNetwork : IDiscoveryBackend` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:469` | `public Task<DiscoveryAdvertisementId> StartAdvertiseAsync(DiscoveryPacket packet, DiscoveryOptions options, Cancell...` | 已核验 | R09+R06+R10 |
| `Net/Discovery/NetDiscovery.cs:482` | `public Task UpdateAdvertiseAsync(DiscoveryAdvertisementId id, DiscoveryPacket packet, DiscoveryOptions options, Can...` | 已核验 | R09+R06+R10 |
| `Net/Discovery/NetDiscovery.cs:496` | `public Task StopAdvertiseAsync(DiscoveryAdvertisementId id, CancellationToken token = default)` | 已核验 | R09+R06 |
| `Net/Discovery/NetDiscovery.cs:507` | `public Task<IReadOnlyList<DiscoveryPacket>> ScanAsync(TimeSpan duration, DiscoveryOptions options, CancellationToke...` | 已核验 | R09+R06+R03+R10 |
| `Net/Discovery/NetDiscovery.cs:516` | `public ValueTask DisposeAsync() => ValueTask.CompletedTask;` | 已核验 | R09+R06+R02 |
| `Net/Discovery/NetDiscovery.cs:522` | `public sealed class UdpDiscoveryNetwork : IDiscoveryBackend` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:535` | `public UdpDiscoveryNetwork(TimeProvider? timeProvider = null)` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:540` | `public async Task<DiscoveryAdvertisementId> StartAdvertiseAsync(DiscoveryPacket packet, DiscoveryOptions options, C...` | 已核验 | R09+R06+R10 |
| `Net/Discovery/NetDiscovery.cs:562` | `public async Task UpdateAdvertiseAsync(DiscoveryAdvertisementId id, DiscoveryPacket packet, DiscoveryOptions option...` | 已核验 | R09+R06+R10 |
| `Net/Discovery/NetDiscovery.cs:582` | `public async Task StopAdvertiseAsync(DiscoveryAdvertisementId id, CancellationToken token = default)` | 已核验 | R09+R06 |
| `Net/Discovery/NetDiscovery.cs:623` | `public Task<IReadOnlyList<DiscoveryPacket>> ScanAsync(TimeSpan duration, DiscoveryOptions options, CancellationToke...` | 已核验 | R09+R06+R03+R10 |
| `Net/Discovery/NetDiscovery.cs:707` | `public ValueTask DisposeAsync()` | 已核验 | R09+R06+R02 |
| `Net/Discovery/NetDiscovery.cs:790` | `public sealed class NetDiscovery : IAsyncDisposable` | 已核验 | R09+R06 |
| `Net/Discovery/NetDiscovery.cs:809` | `public NetDiscovery(GameNetOptions options)` | 已核验 | R09+R10 |
| `Net/Discovery/NetDiscovery.cs:817` | `public NetDiscovery(GameNetOptions options, IDiscoveryBackend backend, NetDiagnostics? diagnostics = null)` | 已核验 | R09+R10 |
| `Net/Discovery/NetDiscovery.cs:831` | `public NetDiagnostics Diagnostics { get; }` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:836` | `public async Task<NetSessionResult> StartAdvertiseAsync<TMetadata>(LanAdvertiseInfo info, TMetadata metadata)` | 已核验 | R09+R06+R10 |
| `Net/Discovery/NetDiscovery.cs:872` | `public async Task<NetSessionResult> UpdateAdvertiseMetadataAsync<TMetadata>(TMetadata metadata)` | 已核验 | R09+R06+R10 |
| `Net/Discovery/NetDiscovery.cs:903` | `public async Task<NetSessionResult> StopAdvertiseAsync()` | 已核验 | R09+R06+R10 |
| `Net/Discovery/NetDiscovery.cs:938` | `public Task<IReadOnlyList<LanScanResult<TMetadata>>> ScanAsync<TMetadata>(` | 本轮修正 | R09+R06+R03+R10 |
| `Net/Discovery/NetDiscovery.cs:954` | `public Task<IReadOnlyList<LanScanResult<TMetadata>>> ScanAsync<TMetadata>(` | 本轮修正 | R09+R06+R03+R10 |
| `Net/Discovery/NetDiscovery.cs:1050` | `public Task<LanBrowser<TMetadata>> StartBrowserAsync<TMetadata>()` | 已核验 | R09+R06 |
| `Net/Discovery/NetDiscovery.cs:1058` | `public Task<LanBrowser<TMetadata>> StartBrowserAsync<TMetadata>(uint metadataSchemaId)` | 已核验 | R09+R06 |
| `Net/Discovery/NetDiscovery.cs:1077` | `public ValueTask DisposeAsync()` | 已核验 | R09+R06+R02 |
| `Net/Discovery/NetDiscovery.cs:1350` | `public sealed record DiscoveryPacket(` | 已核验 | R09+R10 |
| `Net/Discovery/NetDiscovery.cs:1362` | `public const uint ExpectedMagic = 0x53464E44;` | 已核验 | R09+R10 |
| `Net/Discovery/NetDiscovery.cs:1365` | `public const ushort CurrentPacketVersion = 1;` | 已核验 | R09+R10 |
| `Net/Discovery/NetDiscovery.cs:1369` | `public IPEndPoint? RemoteEndPoint { get; init; }` | 已核验 | R09 |
| `Net/Discovery/NetDiscovery.cs:1373` | `public TimeSpan? EstimatedLatency { get; init; }` | 已核验 | R09 |
| `Net/Flow/NetFlow.cs:9` | `public sealed class FlowPolicy` | 已核验 | R08 |
| `Net/Flow/NetFlow.cs:26` | `public string Mode { get; }` | 已核验 | R08 |
| `Net/Flow/NetFlow.cs:31` | `public int Count { get; }` | 已核验 | R08+R03 |
| `Net/Flow/NetFlow.cs:38` | `public static FlowPolicy AllAccepted() => new("all");` | 已核验 | R08 |
| `Net/Flow/NetFlow.cs:43` | `public static FlowPolicy AnyAccepted() => new("any");` | 已核验 | R08 |
| `Net/Flow/NetFlow.cs:48` | `public static FlowPolicy MajorityAccepted() => new("majority");` | 已核验 | R08 |
| `Net/Flow/NetFlow.cs:53` | `public static FlowPolicy Quorum(int count)` | 已核验 | R08 |
| `Net/Flow/NetFlow.cs:64` | `public static FlowPolicy Custom(Func<int, int, bool> evaluator)` | 已核验 | R08 |
| `Net/Flow/NetFlow.cs:75` | `public sealed record FlowPeerResponse<TResponse>(PeerId PeerId, TResponse? Response, bool Accepted, string? Message);` | 已核验 | R08+R10 |
| `Net/Flow/NetFlow.cs:81` | `public sealed class FlowResult<TResponse>` | 已核验 | R08+R10 |
| `Net/Flow/NetFlow.cs:86` | `public long FlowId { get; init; }` | 已核验 | R08 |
| `Net/Flow/NetFlow.cs:91` | `public required FlowEndReason Reason { get; init; }` | 已核验 | R08 |
| `Net/Flow/NetFlow.cs:96` | `public bool Accepted => Reason == FlowEndReason.Accepted;` | 已核验 | R08 |
| `Net/Flow/NetFlow.cs:101` | `public required IReadOnlyList<FlowPeerResponse<TResponse>> Responses { get; init; }` | 已核验 | R08+R03 |
| `Net/Flow/NetFlow.cs:107` | `public sealed class NetFlow` | 已核验 | R08 |
| `Net/Flow/NetFlow.cs:137` | `public IReadOnlyList<long> PendingFlowIds => _pendingFlows.Keys.OrderBy(id => id).ToArray();` | 已核验 | R08+R03 |
| `Net/Flow/NetFlow.cs:142` | `public IReadOnlyList<PeerId> GetPendingPeers(long flowId)` | 已核验 | R08+R03+R13 |
| `Net/Flow/NetFlow.cs:152` | `public Task<NetSendResult> ResendPendingToAsync(long flowId, PeerId peerId)` | 已核验 | R08+R06+R10 |
| `Net/Flow/NetFlow.cs:165` | `public void OnProposal<TProposal, TResponse>(Func<NetContext, TProposal, TResponse> handler)` | 已核验 | R08 |
| `Net/Flow/NetFlow.cs:176` | `public void OnProposal<TProposal, TResponse>(Func<NetContext, TProposal, Task<TResponse>> handler)` | 已核验 | R08+R06 |
| `Net/Flow/NetFlow.cs:187` | `public async Task<FlowResult<TResponse>> ProposeAsync<TProposal, TResponse>(` | 已核验 | R08+R06+R10 |
| `Net/Flow/NetFlow.cs:309` | `public PendingFlow(` | 已核验 | R08 |
| `Net/Flow/NetFlow.cs:329` | `public long FlowId { get; }` | 已核验 | R08 |
| `Net/Flow/NetFlow.cs:330` | `public int TargetCount { get; }` | 已核验 | R08+R03 |
| `Net/Flow/NetFlow.cs:331` | `public TaskCompletionSource<FlowResult<TResponse>> Completion { get; } =` | 已核验 | R08+R06+R10 |
| `Net/Flow/NetFlow.cs:334` | `public IReadOnlyList<PeerId> GetPendingPeers()` | 已核验 | R08+R03+R13 |
| `Net/Flow/NetFlow.cs:340` | `public Task<NetSendResult> ResendPendingToAsync(PeerId peerId)` | 已核验 | R08+R06+R10 |
| `Net/Flow/NetFlow.cs:357` | `public void Start()` | 已核验 | R08 |
| `Net/Flow/NetFlow.cs:365` | `public void Complete(FlowEndReason reason)` | 已核验 | R08 |
| `Net/Messaging/INetCodec.cs:8` | `public interface INetCodec` | 已核验 | R08 |
| `Net/Messaging/INetCodec.cs:24` | `public sealed class JsonNetCodec : INetCodec` | 已核验 | R08 |
| `Net/Messaging/INetCodec.cs:29` | `public byte[] Encode<T>(T message) => JsonSerializer.SerializeToUtf8Bytes(message, _options);` | 已核验 | R08 |
| `Net/Messaging/INetCodec.cs:32` | `public object? Decode(ReadOnlySpan<byte> payload, Type messageType) => JsonSerializer.Deserialize(payload, messageT...` | 已核验 | R08 |
| `Net/Messaging/NetMessageRegistry.cs:12` | `public sealed class NetMessageAttribute : Attribute` | 已核验 | R08+R10 |
| `Net/Messaging/NetMessageRegistry.cs:18` | `public NetMessageAttribute(string key)` | 已核验 | R08+R10 |
| `Net/Messaging/NetMessageRegistry.cs:26` | `public string Key { get; }` | 已核验 | R08 |
| `Net/Messaging/NetMessageRegistry.cs:33` | `public sealed class NetHandlerAttribute : Attribute` | 已核验 | R08+R10 |
| `Net/Messaging/NetMessageRegistry.cs:39` | `public NetHandlerAttribute(Type messageType)` | 已核验 | R08+R10 |
| `Net/Messaging/NetMessageRegistry.cs:47` | `public Type MessageType { get; }` | 已核验 | R08 |
| `Net/Messaging/NetMessageRegistry.cs:54` | `public sealed class NetRequestHandlerAttribute : Attribute` | 已核验 | R08+R10 |
| `Net/Messaging/NetMessageRegistry.cs:61` | `public NetRequestHandlerAttribute(Type requestType, Type responseType)` | 已核验 | R08+R10 |
| `Net/Messaging/NetMessageRegistry.cs:70` | `public Type RequestType { get; }` | 已核验 | R08 |
| `Net/Messaging/NetMessageRegistry.cs:75` | `public Type ResponseType { get; }` | 已核验 | R08 |
| `Net/Messaging/NetMessageRegistry.cs:82` | `public sealed class NetFlowHandlerAttribute : Attribute` | 已核验 | R08+R10 |
| `Net/Messaging/NetMessageRegistry.cs:89` | `public NetFlowHandlerAttribute(Type proposalType, Type responseType)` | 已核验 | R08+R10 |
| `Net/Messaging/NetMessageRegistry.cs:98` | `public Type ProposalType { get; }` | 已核验 | R08 |
| `Net/Messaging/NetMessageRegistry.cs:103` | `public Type ResponseType { get; }` | 已核验 | R08 |
| `Net/Messaging/NetMessageRegistry.cs:109` | `public sealed class NetMessageRegistry` | 已核验 | R08 |
| `Net/Messaging/NetMessageRegistry.cs:124` | `public IReadOnlyList<NetHandlerDescriptor> HandlerDescriptors` | 已核验 | R08+R03 |
| `Net/Messaging/NetMessageRegistry.cs:132` | `public IReadOnlyList<NetRequestHandlerDescriptor> RequestHandlerDescriptors` | 已核验 | R08+R03 |
| `Net/Messaging/NetMessageRegistry.cs:140` | `public IReadOnlyList<NetFlowHandlerDescriptor> FlowHandlerDescriptors` | 已核验 | R08+R03 |
| `Net/Messaging/NetMessageRegistry.cs:148` | `public NetMessageDescriptor Register<T>() => Register(typeof(T));` | 已核验 | R08+R13 |
| `Net/Messaging/NetMessageRegistry.cs:154` | `public NetMessageDescriptor Register(Type messageType)` | 已核验 | R08+R13 |
| `Net/Messaging/NetMessageRegistry.cs:185` | `public NetMessageDescriptor Get<T>() => Get(typeof(T));` | 已核验 | R08+R13 |
| `Net/Messaging/NetMessageRegistry.cs:191` | `public NetMessageDescriptor Get(Type messageType)` | 已核验 | R08+R13 |
| `Net/Messaging/NetMessageRegistry.cs:207` | `public bool TryGet(ulong messageId, out NetMessageDescriptor descriptor)` | 已核验 | R08+R13 |
| `Net/Messaging/NetMessageRegistry.cs:249` | `public void RegisterAssembly(Assembly assembly, Func<Type, bool>? typeFilter = null)` | 已核验 | R08+R13 |
| `Net/Messaging/NetMessageRegistry.cs:329` | `public string GetFingerprint()` | 已核验 | R08+R13 |
| `Net/Messaging/NetMessageRegistry.cs:390` | `public NetFingerprintCheckResult CheckFingerprint(string remoteFingerprint, NetFingerprintPolicy policy)` | 已核验 | R08+R10 |
| `Net/Messaging/NetMessageRegistry.cs:457` | `public RegistryReservation(NetMessageRegistry owner, bool completed = false)` | 已核验 | R08 |
| `Net/Messaging/NetMessageRegistry.cs:463` | `public void Commit()` | 已核验 | R08 |
| `Net/Messaging/NetMessageRegistry.cs:471` | `public void Dispose()` | 已核验 | R08+R02 |
| `Net/Messaging/NetMessageRegistry.cs:484` | `public sealed record NetMessageDescriptor(Type MessageType, string Key, ulong MessageId);` | 已核验 | R08+R10 |
| `Net/Messaging/NetMessageRegistry.cs:489` | `public sealed record NetHandlerDescriptor(Type MessageType, MethodInfo Method);` | 已核验 | R08+R10 |
| `Net/Messaging/NetMessageRegistry.cs:494` | `public sealed record NetRequestHandlerDescriptor(Type RequestType, Type ResponseType, MethodInfo Method);` | 已核验 | R08+R10 |
| `Net/Messaging/NetMessageRegistry.cs:499` | `public sealed record NetFlowHandlerDescriptor(Type ProposalType, Type ResponseType, MethodInfo Method);` | 已核验 | R08+R10 |
| `Net/Messaging/NetMessageRegistry.cs:504` | `public enum NetFingerprintPolicy` | 已核验 | R08+R10 |
| `Net/Messaging/NetMessageRegistry.cs:514` | `public enum NetFingerprintStatus` | 已核验 | R08+R10 |
| `Net/Messaging/NetMessageRegistry.cs:525` | `public readonly record struct NetFingerprintCheckResult(NetFingerprintStatus Status, string? Message);` | 已核验 | R08+R10 |
| `Net/Messaging/NetPacket.cs:5` | `public const string Message = "message";` | 已核验 | R08+R10 |
| `Net/Messaging/NetPacket.cs:6` | `public const string Request = "request";` | 已核验 | R08+R10 |
| `Net/Messaging/NetPacket.cs:7` | `public const string Response = "response";` | 已核验 | R08+R10 |
| `Net/Messaging/NetPacket.cs:8` | `public const string Relay = "relay";` | 已核验 | R08+R10 |
| `Net/Messaging/NetPacket.cs:9` | `public const string RelayResult = "relay.result";` | 已核验 | R08+R10 |
| `Net/Messaging/NetPacket.cs:11` | `public required string Kind { get; init; }` | 已核验 | R08 |
| `Net/Messaging/NetPacket.cs:12` | `public ulong MessageId { get; init; }` | 已核验 | R08 |
| `Net/Messaging/NetPacket.cs:13` | `public PeerId SenderId { get; init; }` | 已核验 | R08 |
| `Net/Messaging/NetPacket.cs:14` | `public PeerId TargetPeerId { get; init; }` | 已核验 | R08 |
| `Net/Messaging/NetPacket.cs:15` | `public byte[] Payload { get; init; } = Array.Empty<byte>();` | 已核验 | R08 |
| `Net/Messaging/NetPacket.cs:16` | `public long CorrelationId { get; init; }` | 已核验 | R08 |
| `Net/Messaging/NetPacket.cs:17` | `public ulong ResponseMessageId { get; init; }` | 已核验 | R08 |
| `Net/Messaging/NetPacket.cs:18` | `public NetRequestStatus RequestStatus { get; init; } = NetRequestStatus.Ok;` | 已核验 | R08+R10 |
| `Net/Messaging/NetPacket.cs:19` | `public NetSendStatus SendStatus { get; init; } = NetSendStatus.Ok;` | 已核验 | R08+R10 |
| `Net/Messaging/NetPacket.cs:20` | `public string? Error { get; init; }` | 已核验 | R08 |
| `Net/Messaging/NetPacket.cs:27` | `public sealed record NetContext(PeerId SenderId);` | 已核验 | R08+R10 |
| `Net/Messaging/NetPacket.cs:34` | `public sealed record NetRelayContext(PeerId SenderId, PeerId TargetPeerId);` | 已核验 | R08+R10 |
| `Net/Messaging/NetMessenger.cs:10` | `public sealed class NetMessenger` | 已核验 | R08 |
| `Net/Messaging/NetMessenger.cs:65` | `public NetMessageRegistry Registry { get; } = new();` | 已核验 | R08 |
| `Net/Messaging/NetMessenger.cs:70` | `public NetMessageDescriptor RegisterMessage<T>()` | 已核验 | R08+R13 |
| `Net/Messaging/NetMessenger.cs:82` | `public void On<T>(Action<NetContext, T> handler)` | 已核验 | R08 |
| `Net/Messaging/NetMessenger.cs:96` | `public void On<T>(Func<NetContext, T, Task> handler)` | 已核验 | R08+R06 |
| `Net/Messaging/NetMessenger.cs:122` | `public void OnRequest<TRequest, TResponse>(Func<NetContext, TRequest, TResponse> handler)` | 已核验 | R08 |
| `Net/Messaging/NetMessenger.cs:135` | `public void OnRequest<TRequest, TResponse>(Func<NetContext, TRequest, Task<TResponse>> handler)` | 已核验 | R08+R06 |
| `Net/Messaging/NetMessenger.cs:161` | `public void RegisterAssemblyHandlers(Assembly assembly, object? target = null, Func<Type, object>? targetFactory = ...` | 已核验 | R08+R13 |
| `Net/Messaging/NetMessenger.cs:311` | `public void AllowRelay<T>(Func<NetRelayContext, T, bool> policy)` | 已核验 | R08 |
| `Net/Messaging/NetMessenger.cs:350` | `public async ValueTask<NetSendResult> SendToServerAsync<T>(T message)` | 已核验 | R08+R06+R10 |
| `Net/Messaging/NetMessenger.cs:366` | `public async ValueTask<NetSendResult> SendAsync<T>(PeerId peerId, T message)` | 已核验 | R08+R06+R10 |
| `Net/Messaging/NetMessenger.cs:387` | `public async ValueTask<NetSendResult> BroadcastAsync<T>(T message)` | 已核验 | R08+R06+R10 |
| `Net/Messaging/NetMessenger.cs:412` | `public async Task<NetSendResult> RelayAsync<T>(` | 已核验 | R08+R06+R10 |
| `Net/Messaging/NetMessenger.cs:489` | `public async Task<NetRequestResult<TResponse>> RequestAsync<TRequest, TResponse>(` | 已核验 | R08+R06+R10 |
| `Net/Messaging/NetMessenger.cs:1335` | `public ProtocolManifestReservation(` | 已核验 | R08 |
| `Net/Messaging/NetMessenger.cs:1345` | `public void Commit()` | 已核验 | R08 |
| `Net/Messaging/NetMessenger.cs:1353` | `public void Dispose()` | 已核验 | R08+R02 |
| `Net/Messaging/NetMessenger.cs:1364` | `public int InFlightPackets { get; set; }` | 已核验 | R08 |
| `Net/Messaging/NetMessenger.cs:1365` | `public int InFlightBytes { get; set; }` | 已核验 | R08 |
| `Net/Messaging/NetMessenger.cs:1366` | `public DateTimeOffset WindowStartedAt { get; set; }` | 已核验 | R08 |
| `Net/Messaging/NetMessenger.cs:1367` | `public int SentInWindow { get; set; }` | 已核验 | R08 |
| `Net/Messaging/NetMessenger.cs:1372` | `public PeerId PeerId { get; } = peerId;` | 已核验 | R08 |
| `Net/Messaging/NetMessenger.cs:1373` | `public ulong ResponseMessageId { get; } = responseMessageId;` | 已核验 | R08 |
| `Net/Messaging/NetMessenger.cs:1375` | `public TaskCompletionSource<PendingResponse> Completion { get; } =` | 已核验 | R08+R06 |
| `Net/Session/SessionPackets.cs:16` | `public const string JoinRequest = "session.join.request";` | 已核验 | R08+R10 |
| `Net/Session/SessionPackets.cs:17` | `public const string JoinAccepted = "session.join.accepted";` | 已核验 | R08+R10 |
| `Net/Session/SessionPackets.cs:18` | `public const string JoinRejected = "session.join.rejected";` | 已核验 | R08+R10 |
| `Net/Session/SessionPackets.cs:19` | `public const string PeerDirectoryUpdated = "session.peer_directory.updated";` | 已核验 | R08+R10 |
| `Net/Session/SessionPackets.cs:20` | `public const string DisconnectNotice = "session.disconnect.notice";` | 已核验 | R08+R10 |
| `Net/Session/SessionOptions.cs:6` | `public sealed class HostOptions` | 已核验 | R08+R10 |
| `Net/Session/SessionOptions.cs:11` | `public System.Net.IPAddress? BindAddress { get; init; }` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:16` | `public int Port { get; init; }` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:21` | `public int MaxPeers { get; init; } = 8;` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:26` | `public Func<AuthContext, Task<AuthResult>>? Authenticator { get; init; }` | 已核验 | R08+R06+R10 |
| `Net/Session/SessionOptions.cs:31` | `public ReconnectPolicy ReconnectPolicy { get; init; } = ReconnectPolicy.Disabled;` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:37` | `public sealed class JoinOptions` | 已核验 | R08+R10 |
| `Net/Session/SessionOptions.cs:42` | `public required string Host { get; init; }` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:47` | `public int Port { get; init; }` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:52` | `public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:57` | `public byte[]? AuthPayload { get; init; }` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:62` | `public string? ReconnectToken { get; init; }` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:67` | `public ReconnectPolicy Reconnect { get; init; } = ReconnectPolicy.Disabled;` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:73` | `public sealed class ReconnectPolicy` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:78` | `public static ReconnectPolicy Disabled { get; } = new(false, TimeSpan.Zero);` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:83` | `public static ReconnectPolicy Enabled(TimeSpan graceWindow)` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:94` | `public static ReconnectPolicy FixedRetry(int attempts, TimeSpan interval)` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:115` | `public bool IsEnabled { get; }` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:120` | `public TimeSpan GraceWindow { get; }` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:125` | `public int Attempts { get; }` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:130` | `public TimeSpan Interval { get; }` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:136` | `public sealed class AuthContext` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:141` | `public required TransportConnectionId ConnectionId { get; init; }` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:146` | `public byte[]? AuthPayload { get; init; }` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:152` | `public readonly record struct AuthResult(bool Succeeded, string? Message = null)` | 已核验 | R08+R10 |
| `Net/Session/SessionOptions.cs:157` | `public static AuthResult Ok() => new(true);` | 已核验 | R08+R10 |
| `Net/Session/SessionOptions.cs:162` | `public static AuthResult Fail(string message) => new(false, message);` | 已核验 | R08+R10 |
| `Net/Session/SessionOptions.cs:167` | `public static AuthResult Reject(string message) => new(false, message);` | 已核验 | R08+R10 |
| `Net/Session/SessionOptions.cs:173` | `public readonly record struct JoinResult` | 已核验 | R08+R10 |
| `Net/Session/SessionOptions.cs:175` | `public JoinResult(` | 已核验 | R08+R10 |
| `Net/Session/SessionOptions.cs:192` | `public NetSessionStatus Status { get; }` | 已核验 | R08+R10 |
| `Net/Session/SessionOptions.cs:197` | `public PeerId PeerId { get; }` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:202` | `public string? Message { get; }` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:207` | `public string? ReconnectToken { get; }` | 已核验 | R08 |
| `Net/Session/SessionOptions.cs:212` | `public IReadOnlyCollection<PeerInfo> PeerDirectorySnapshot { get; }` | 已核验 | R08+R03 |
| `Net/Session/SessionOptions.cs:217` | `public bool Succeeded => Status == NetSessionStatus.Ok;` | 已核验 | R08+R10 |
| `Net/Session/PeerDirectory.cs:6` | `public sealed record PeerInfo(PeerId PeerId, bool IsServer, bool IsLocal, DateTimeOffset JoinedAt, bool IsConnected...` | 已核验 | R08+R10 |
| `Net/Session/PeerDirectory.cs:11` | `public sealed class PeerDirectory` | 已核验 | R08 |
| `Net/Session/PeerDirectory.cs:19` | `public PeerId LocalPeerId { get; private set; } = PeerId.None;` | 已核验 | R08 |
| `Net/Session/PeerDirectory.cs:24` | `public IReadOnlyCollection<PeerInfo> Peers` | 已核验 | R08+R03 |
| `Net/Session/PeerDirectory.cs:36` | `public PeerInfo? Get(PeerId peerId)` | 已核验 | R08+R13 |
| `Net/Session/PeerDirectory.cs:45` | `public bool IsLocal(PeerId peerId)` | 已核验 | R08 |
| `Net/Session/PeerDirectory.cs:54` | `public bool IsServer(PeerId peerId)` | 已核验 | R08 |
| `Net/Session/PeerDirectory.cs:102` | `public IReadOnlyCollection<PeerInfo> Participants()` | 已核验 | R08+R03 |
| `Net/Session/PeerDirectory.cs:111` | `public IReadOnlyCollection<PeerInfo> RemoteParticipants()` | 已核验 | R08+R03 |
| `Net/Session/PeerDirectory.cs:120` | `public IReadOnlyCollection<PeerId> RemoteParticipantIds()` | 已核验 | R08+R03 |
| `Net/Session/NetSession.cs:6` | `public sealed class NetSession` | 已核验 | R08 |
| `Net/Session/NetSession.cs:11` | `public event Action<NetSessionStateChanged>? StateChanged;` | 已核验 | R08+R02 |
| `Net/Session/NetSession.cs:16` | `public event Action<NetPeerJoined>? PeerJoined;` | 已核验 | R08+R02 |
| `Net/Session/NetSession.cs:21` | `public event Action<NetPeerDisconnected>? PeerDisconnected;` | 已核验 | R08+R02 |
| `Net/Session/NetSession.cs:26` | `public event Action<NetPeerLeft>? PeerLeft;` | 已核验 | R08+R02 |
| `Net/Session/NetSession.cs:31` | `public event Action<NetPeerReconnected>? PeerReconnected;` | 已核验 | R08+R02 |
| `Net/Session/NetSession.cs:36` | `public event Action<NetServerClosed>? ServerClosed;` | 已核验 | R08+R02 |
| `Net/Session/NetSession.cs:41` | `public NetSessionRole Role { get; private set; }` | 已核验 | R08 |
| `Net/Session/NetSession.cs:46` | `public bool IsRunning => Role != NetSessionRole.None;` | 已核验 | R08 |
| `Net/Session/NetSession.cs:69` | `public sealed record NetSessionStateChanged(NetSessionRole OldRole, NetSessionRole NewRole);` | 已核验 | R08+R10 |
| `Net/Session/NetSession.cs:74` | `public sealed record NetPeerJoined(PeerInfo Peer);` | 已核验 | R08+R10 |
| `Net/Session/NetSession.cs:79` | `public sealed record NetPeerDisconnected(PeerId PeerId, DisconnectReason Reason);` | 已核验 | R08+R10 |
| `Net/Session/NetSession.cs:84` | `public sealed record NetPeerLeft(PeerId PeerId, DisconnectReason Reason);` | 已核验 | R08+R10 |
| `Net/Session/NetSession.cs:89` | `public sealed record NetPeerReconnected(PeerId PeerId);` | 已核验 | R08+R10 |
| `Net/Session/NetSession.cs:94` | `public sealed record NetServerClosed(DisconnectReason Reason);` | 已核验 | R08+R10 |
| `Net/Session/NetSession.cs:99` | `public enum NetSessionRole` | 已核验 | R08+R10 |
| `Net/Stats/NetStats.cs:8` | `public enum NetStatsStatus` | 已核验 | R08+R10 |
| `Net/Stats/NetStats.cs:23` | `public sealed class NetPeerStats` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:28` | `public TimeSpan? Rtt { get; init; }` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:33` | `public TimeSpan? AverageRtt { get; init; }` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:38` | `public TimeSpan? Jitter { get; init; }` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:43` | `public double ProbeLoss { get; init; }` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:48` | `public long TimeoutCount { get; init; }` | 已核验 | R08+R03 |
| `Net/Stats/NetStats.cs:53` | `public double? TransportLoss { get; init; }` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:58` | `public DateTimeOffset LastSeenAt { get; init; }` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:64` | `public sealed class NetStatsResult` | 已核验 | R08+R10 |
| `Net/Stats/NetStats.cs:69` | `public required NetStatsStatus Status { get; init; }` | 已核验 | R08+R10 |
| `Net/Stats/NetStats.cs:74` | `public required NetPeerStats PeerStats { get; init; }` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:79` | `public string? Message { get; init; }` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:85` | `public sealed class NetStats` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:109` | `public bool DropProbeResponses { get; set; }` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:114` | `public Task<NetStatsResult> GetLatencyAsync(PeerId peerId)` | 已核验 | R08+R06+R13+R10 |
| `Net/Stats/NetStats.cs:122` | `public async Task<NetStatsResult> GetLatencyAsync(PeerId peerId, TimeSpan timeout, CancellationToken token = default)` | 已核验 | R08+R06+R13+R10 |
| `Net/Stats/NetStats.cs:193` | `public NetPeerStats GetPeerStats(PeerId peerId)` | 已核验 | R08+R13 |
| `Net/Stats/NetStats.cs:292` | `public PendingProbe(PeerId peerId, DateTimeOffset sentAt)` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:298` | `public PeerId PeerId { get; }` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:299` | `public DateTimeOffset SentAt { get; }` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:300` | `public TaskCompletionSource<NetPong> Completion { get; } =` | 已核验 | R08+R06 |
| `Net/Stats/NetStats.cs:306` | `public TimeSpan? Rtt { get; set; }` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:307` | `public TimeSpan? AverageRtt { get; set; }` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:308` | `public TimeSpan? Jitter { get; set; }` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:309` | `public long ProbeCount { get; set; }` | 已核验 | R08+R03 |
| `Net/Stats/NetStats.cs:310` | `public long ProbeFailures { get; set; }` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:311` | `public long TimeoutCount { get; set; }` | 已核验 | R08+R03 |
| `Net/Stats/NetStats.cs:312` | `public int SuccessCount { get; set; }` | 已核验 | R08+R03 |
| `Net/Stats/NetStats.cs:313` | `public DateTimeOffset LastSeenAt { get; set; }` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:315` | `public NetPeerStats ToSnapshot()` | 已核验 | R08 |
| `Net/Stats/NetStats.cs:332` | `public NetStatsStatus Status { get; } = status;` | 已核验 | R08+R10 |
| `Net/Transports/TcpNetTransport.cs:20` | `public static DefaultTcpFrameWriter Instance { get; } = new();` | 已核验 | R08 |
| `Net/Transports/TcpNetTransport.cs:26` | `public async ValueTask WriteAsync(` | 已核验 | R08+R06 |
| `Net/Transports/TcpNetTransport.cs:44` | `public sealed class TcpNetTransport : INetTransport` | 已核验 | R08 |
| `Net/Transports/TcpNetTransport.cs:63` | `public TcpNetTransport(int maxFrameSize = 64 * 1024, TimeProvider? timeProvider = null)` | 已核验 | R08 |
| `Net/Transports/TcpNetTransport.cs:82` | `public int MaxFrameSize` | 已核验 | R08 |
| `Net/Transports/TcpNetTransport.cs:96` | `public TimeProvider TimeProvider` | 已核验 | R08 |
| `Net/Transports/TcpNetTransport.cs:105` | `public IPEndPoint? LocalEndPoint { get; private set; }` | 已核验 | R08 |
| `Net/Transports/TcpNetTransport.cs:108` | `public event Action<TransportPeerConnected>? PeerConnected;` | 已核验 | R08+R02 |
| `Net/Transports/TcpNetTransport.cs:111` | `public event Action<TransportPeerDisconnected>? PeerDisconnected;` | 已核验 | R08+R02 |
| `Net/Transports/TcpNetTransport.cs:114` | `public event Action<TransportPacketReceived>? PacketReceived;` | 已核验 | R08+R02 |
| `Net/Transports/TcpNetTransport.cs:117` | `public event Action<TransportError>? Error;` | 已核验 | R08+R02 |
| `Net/Transports/TcpNetTransport.cs:120` | `public async Task<TransportStartResult> StartServerAsync(NetListenOptions options, CancellationToken token = default)` | 已核验 | R08+R06+R10 |
| `Net/Transports/TcpNetTransport.cs:170` | `public async Task<TransportConnectResult> ConnectAsync(NetConnectOptions options, CancellationToken token = default)` | 已核验 | R08+R06+R10 |
| `Net/Transports/TcpNetTransport.cs:223` | `public async Task<TransportStartResult> StopServerAsync(CancellationToken token = default)` | 已核验 | R08+R06+R10 |
| `Net/Transports/TcpNetTransport.cs:254` | `public Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.LocalClo...` | 已核验 | R08+R06 |
| `Net/Transports/TcpNetTransport.cs:269` | `public async ValueTask<NetSendResult> SendAsync(TransportConnectionId connectionId, ReadOnlyMemory<byte> data, NetC...` | 已核验 | R08+R06+R10 |
| `Net/Transports/TcpNetTransport.cs:324` | `public ValueTask DisposeAsync()` | 已核验 | R08+R06+R02 |
| `Net/Transports/TcpNetTransport.cs:644` | `public TcpConnection(TcpClient client)` | 已核验 | R08 |
| `Net/Transports/TcpNetTransport.cs:650` | `public NetworkStream Stream { get; }` | 已核验 | R08 |
| `Net/Transports/TcpNetTransport.cs:651` | `public byte[] ReadLengthBuffer { get; } = new byte[sizeof(int)];` | 已核验 | R08 |
| `Net/Transports/TcpNetTransport.cs:652` | `public byte[] WriteLengthBuffer { get; } = new byte[sizeof(int)];` | 已核验 | R08 |
| `Net/Transports/TcpNetTransport.cs:653` | `public SemaphoreSlim WriteLock { get; } = new(1, 1);` | 已核验 | R08 |
| `Net/Transports/TcpNetTransport.cs:654` | `public Task ReadTask => _readTaskReady.Task.Unwrap();` | 已核验 | R08+R06 |
| `Net/Transports/TcpNetTransport.cs:656` | `public void SetReadTask(Task readTask)` | 已核验 | R08+R06 |
| `Net/Transports/TcpNetTransport.cs:661` | `public bool TryEnterSend()` | 已核验 | R08+R13 |
| `Net/Transports/TcpNetTransport.cs:673` | `public void ExitSend()` | 已核验 | R08 |
| `Net/Transports/TcpNetTransport.cs:686` | `public void Dispose()` | 已核验 | R08+R02 |
| `Net/Transports/MemoryNetTransport.cs:8` | `public sealed class MemoryNetNetwork` | 已核验 | R08 |
| `Net/Transports/MemoryNetTransport.cs:18` | `public MemoryNetTransport CreateTransport(string name) => new(this, name);` | 已核验 | R08 |
| `Net/Transports/MemoryNetTransport.cs:86` | `public sealed class MemoryNetTransport : INetTransport` | 已核验 | R08 |
| `Net/Transports/MemoryNetTransport.cs:110` | `public event Action<TransportPeerConnected>? PeerConnected;` | 已核验 | R08+R02 |
| `Net/Transports/MemoryNetTransport.cs:113` | `public event Action<TransportPeerDisconnected>? PeerDisconnected;` | 已核验 | R08+R02 |
| `Net/Transports/MemoryNetTransport.cs:116` | `public event Action<TransportPacketReceived>? PacketReceived;` | 已核验 | R08+R02 |
| `Net/Transports/MemoryNetTransport.cs:119` | `public event Action<TransportError>? Error;` | 已核验 | R08+R02 |
| `Net/Transports/MemoryNetTransport.cs:122` | `public Task<TransportStartResult> StartServerAsync(NetListenOptions options, CancellationToken token = default)` | 已核验 | R08+R06+R10 |
| `Net/Transports/MemoryNetTransport.cs:149` | `public Task<TransportConnectResult> ConnectAsync(NetConnectOptions options, CancellationToken token = default)` | 已核验 | R08+R06+R10 |
| `Net/Transports/MemoryNetTransport.cs:167` | `public async Task<TransportStartResult> StopServerAsync(CancellationToken token = default)` | 已核验 | R08+R06+R10 |
| `Net/Transports/MemoryNetTransport.cs:202` | `public async Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.Lo...` | 已核验 | R08+R06 |
| `Net/Transports/MemoryNetTransport.cs:220` | `public ValueTask<NetSendResult> SendAsync(TransportConnectionId connectionId, ReadOnlyMemory<byte> data, NetChannel...` | 已核验 | R08+R06+R10 |
| `Net/Transports/MemoryNetTransport.cs:237` | `public ValueTask DisposeAsync()` | 已核验 | R08+R06+R02 |
| `Net/Transports/INetTransport.cs:6` | `public interface INetTransport : IAsyncDisposable` | 已核验 | R08+R06 |
| `Net/Transports/INetTransport.cs:57` | `public sealed class NetListenOptions` | 已核验 | R08+R10 |
| `Net/Transports/INetTransport.cs:62` | `public System.Net.IPAddress? BindAddress { get; init; }` | 已核验 | R08 |
| `Net/Transports/INetTransport.cs:67` | `public required int Port { get; init; }` | 已核验 | R08 |
| `Net/Transports/INetTransport.cs:72` | `public int MaxConnections { get; init; } = 32;` | 已核验 | R08 |
| `Net/Transports/INetTransport.cs:78` | `public sealed class NetConnectOptions` | 已核验 | R08+R10 |
| `Net/Transports/INetTransport.cs:83` | `public required string Host { get; init; }` | 已核验 | R08 |
| `Net/Transports/INetTransport.cs:88` | `public required int Port { get; init; }` | 已核验 | R08 |
| `Net/Transports/INetTransport.cs:93` | `public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);` | 已核验 | R08 |
| `Net/Transports/INetTransport.cs:99` | `public readonly record struct TransportStartResult(NetTransportStatus Status, string? Message = null);` | 已核验 | R08+R10 |
| `Net/Transports/INetTransport.cs:104` | `public readonly record struct TransportConnectResult(NetTransportStatus Status, TransportConnectionId ConnectionId,...` | 已核验 | R08+R10 |
| `Net/Transports/INetTransport.cs:109` | `public readonly record struct TransportPeerConnected(TransportConnectionId ConnectionId);` | 已核验 | R08+R10 |
| `Net/Transports/INetTransport.cs:114` | `public readonly record struct TransportPeerDisconnected(TransportConnectionId ConnectionId, DisconnectReason Reason);` | 已核验 | R08+R10 |
| `Net/Transports/INetTransport.cs:119` | `public readonly record struct TransportPacketReceived(TransportConnectionId ConnectionId, ReadOnlyMemory<byte> Data...` | 已核验 | R08+R10 |
| `Net/Transports/INetTransport.cs:124` | `public readonly record struct TransportError(TransportConnectionId ConnectionId, string Message, Exception? Excepti...` | 已核验 | R08+R10 |
| `Net/Transports/INetTransport.cs:129` | `public enum DisconnectReason` | 已核验 | R08+R10 |



## 计数校验

以下只读 PowerShell 校验 exact rg 输出、ledger 行数和模块分组：

```powershell
$matches = rg -n "^public |^\s+public " -g '*.cs' -g '!Test/**' -g '!**/bin/**' -g '!**/obj/**'
if ($matches.Count -ne 1061) { throw "public inventory count: $($matches.Count)" }
$ledger = Select-String -Path '.\docs\superpowers\reviews\2026-07-10-round-6-public-inventory.md' -Pattern '^\| `[^`]+:\d+` \|'
if ($ledger.Count -ne $matches.Count) { throw "ledger count: $($ledger.Count)" }
$groups = $matches | ForEach-Object {
    $path = (($_ -split ':', 3)[0] -replace '\\', '/')
    if ($path -notmatch '/') { 'Core' } elseif ($path -like 'FrameworkImpl/*') { 'FrameworkImpl' } else { ($path -split '/')[0] }
} | Group-Object -NoElement | Sort-Object Name
$groups | Format-Table Name, Count -AutoSize
```
