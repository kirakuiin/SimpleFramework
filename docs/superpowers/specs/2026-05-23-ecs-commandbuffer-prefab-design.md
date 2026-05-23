# ECS CommandBuffer、Prefab 与遍历语法糖设计

## 背景

当前 ECS 核心存储由 `World`、`Archetype`、`TypeSignature`、`Query` 和强类型组件列组成。`Query` 枚举期间会记录 `World.StructuralVersion`；如果遍历中发生创建实体、销毁实体、添加新组件、移除组件等结构变更，会抛出异常。这保护了 Archetype 行索引和组件引用的正确性。

本批次目标是在不重构核心存储的前提下，补齐 ECS 边界测试，并新增三个小型 API：

- `CommandBuffer`：延迟播放结构变更，作为 Query 遍历期间修改结构的官方解法。
- `EntityPrefab`：支持“单实体 + 默认组件值”的第一版预制体。
- `Each<T>`、`Each<T1,T2>`：提供轻量遍历语法糖，但不改变结构变更规则。

`SystemGroup`、自动依赖排序、子实体 Prefab、序列化和资源引用不进入本批次实现范围。

## 设计目标

1. 固定当前 ECS 的边界行为，避免 CommandBuffer 和 Prefab 引入隐式语义漂移。
2. 保持 `World` 作为实体生命周期、组件访问和结构迁移的唯一执行者。
3. 让 Query 遍历期间的结构修改通过 CommandBuffer 延迟执行。
4. Prefab 第一版只表达“单实体 + 默认组件值”。
5. Each API 只做语法糖，不绕过 Query 的结构版本检查。

## 边界行为与测试

新增或扩展 `Test/ECS` 下的测试，锁定这些行为：

- 添加新组件迁移 Archetype 时保留旧组件值。
- `Query<T>().Not<TExclude>()` 在实体新增排除组件后不再返回该实体。
- 移除 include 组件后，原 Query 不再返回该实体。
- `Set<T>` 更新已有组件且不迁移 Archetype。
- stale、foreign、invalid、destroyed entity 的 `Add`、`Remove`、`Get`、`Set`、`Has`、`TryGet`、`DestroyEntity` 行为一致。
- 创建实体时传入重复组件类型抛 `ArgumentException`，错误信息包含重复组件类型名。

行为约定：

- `Has<T>`、`TryGet<T>`、`DestroyEntity` 对无效实体返回 `false`。
- `Get<T>`、`Add<T>`、`Set<T>`、`Remove<T>` 对无效实体抛 `InvalidOperationException`。
- `Set<T>` 对缺失组件抛 `InvalidOperationException`。
- `Remove<T>` 对有效但缺失组件的实体返回 `false`。

## CommandBuffer

新增 `CommandBuffer` 类，构造时绑定一个 `World`：

```csharp
var buffer = new CommandBuffer(world);
```

支持记录并延迟播放：

```csharp
buffer.CreateEntity(...);
buffer.DestroyEntity(entity);
buffer.Add(entity, component);
buffer.Set(entity, component);
buffer.Remove<T>(entity);
buffer.Playback();
```

`Playback()` 按记录顺序执行。执行语义沿用 `World` 本身：

- `DestroyEntity` 对无效实体返回 `false`，不会中断播放。
- `Add`、`Set`、`Remove` 对无效实体抛异常，播放停止并暴露错误。
- 对已销毁实体执行后续 `Set` 或 `Remove` 时，同样按 `World` 的无效实体规则处理。

延迟创建实体使用轻量占位句柄：

```csharp
var created = buffer.CreateEntity(new Position());
buffer.Add(created, new Velocity());
var result = buffer.Playback();
var entity = result.Resolve(created);
```

设计要点：

- `BufferedEntity` 只在创建它的 CommandBuffer 内有效。
- `CommandBuffer` 的 `Add`、`Set`、`Remove`、`DestroyEntity` 同时支持真实 `Entity` 和 `BufferedEntity`。
- `Playback()` 返回 `CommandBufferResult`，用于把 `BufferedEntity` 解析为真实 `Entity`。
- 如果占位实体在同一个 buffer 内被销毁，`Resolve` 仍返回创建出的真实句柄，但该实体播放完成后可能已经不存活。
- 第一版不做跨 buffer 占位引用，也不做命令合并优化。

## EntityPrefab

新增 `EntityPrefab`，只保存一组默认组件值：

```csharp
var prefab = EntityPrefab.Create()
    .With(new Position())
    .With(new Health { Current = 100 });

var entity = world.Instantiate(prefab);
```

设计要点：

- `EntityPrefab.Create()` 创建空 Prefab。
- `With<T>(T component)` 返回同一个 Prefab，便于链式构造。
- 同一 Prefab 内重复组件类型抛 `ArgumentException`。
- `World.Instantiate(EntityPrefab prefab)` 创建一个实体，组件值来自 Prefab。
- Prefab 不支持子实体、覆盖参数、序列化、资源引用。

组件复制语义保持与当前 `World.CreateEntity` 一致：结构体组件按值复制；类组件按引用传入。第一版不引入 clone 接口，避免要求所有组件实现额外协议。

## Each 语法糖

新增 ref 委托，避免使用不能表达 ref 参数的 `Action<>`：

```csharp
public delegate void EachRef<T1>(Entity entity, ref T1 c1) where T1 : IComponent;

public delegate void EachRef<T1, T2>(Entity entity, ref T1 c1, ref T2 c2)
    where T1 : IComponent
    where T2 : IComponent;
```

在 `World` 上提供：

```csharp
world.Each<Position>((Entity entity, ref Position position) =>
{
    position.X += 1;
});

world.Each<Position, Velocity>((Entity entity, ref Position position, ref Velocity velocity) =>
{
    position.X += velocity.X;
});
```

实现只包装：

- `Query<T1>()` 或 `Query<T1,T2>()`
- 对每个实体调用 `Get<T>()` 获取 ref 组件
- 执行用户委托

如果委托内部直接调用 `World.Add`、`World.Remove`、`World.DestroyEntity` 或 `World.CreateEntity` 导致结构变更，仍由 Query 的结构版本检查抛 `InvalidOperationException`。需要结构变更时使用 `CommandBuffer`。

## 文件与项目边界

新增代码放在 `ECS/` 项目内，命名空间保持 `SimpleFramework.ECS`：

- `CommandBuffer.cs`
- `EntityPrefab.cs`
- `ECSExtension.cs`

测试放在 `Test/ECS/`：

- `UnitTestCommandBuffer.cs`
- `UnitTestEntityPrefab.cs`
- `UnitTestEach.cs`
- 扩展 `UnitTestWorld.cs` 和 `UnitTestQuery.cs`

公共 API XML 注释使用中文。

## 验证

实现后运行：

```powershell
dotnet test .\SimpleFramework.sln --no-restore -m:1 /nr:false
```

并扫描 ECS 乱码：

```powershell
rg -n "瀛|鎸|绯|涓嶅|銆|€" ECS
```

如果测试超时，先清理残留 `dotnet`、`testhost`、`vstest` 进程，再定位卡住原因。

## 非目标

- 不公开 Archetype 的结构修改接口。
- 不改变 `World.Destroy()` 保留 slot 并使旧句柄失效的设计。
- 不优化结构迁移中的装箱路径。
- 不实现 SystemGroup、手动排序或自动依赖排序。
- 不允许 Query 枚举中直接结构变更；CommandBuffer 是官方解法。
