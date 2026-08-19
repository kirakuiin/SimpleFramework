# v1.1 Domain 生命周期与注册迁移指南

v1.1 对 Domain 生命周期和组件注册做一次原子切换，不提供旧语义兼容开关。迁移目标是消除多键别名、生命周期复用和隐式状态带来的不确定性。

## 1. 每个实例只注册一次

旧代码可能为了同时按接口和具体类型访问而重复注册：

```csharp
domain.RegisterModel(player);
domain.RegisterModelAs<IPlayer>(player);
```

v1.1 会拒绝第二次注册。通常只注册具体类型即可：

```csharp
domain.RegisterModel(player);

var concrete = domain.RequireModel<Player>();
var service = domain.RequireModel<IPlayer>(); // 唯一可赋值候选项
```

如果同一接口有多个实现，并希望指定默认服务，选择接口作为唯一精确主键：

```csharp
domain.RegisterModel(new LocalPlayer());
domain.RegisterModelAs<IPlayer>(new NetworkPlayer());

var service = domain.RequireModel<IPlayer>(); // 精确键 NetworkPlayer
```

服务接口仍必须继承对应标记接口，例如 `IPlayer : IModel`。

## 2. 处理可赋值歧义

没有精确键且多个本地实例兼容请求类型时，`Get*`、`TryGet*` 和 `Require*` 都会抛出 `AmbiguousComponentException`。使用 `RequestedType` 和 `CandidateKeys` 定位冲突，然后：

- 删除不必要的注册；或
- 用 `RegisterModelAs<TService>` / `RegisterSystemAs<TService>` 建立唯一精确主键；或
- 请求更具体的类型。

## 3. 不再复用 System/Model 实例

System/Model 一旦开始初始化，便永久绑定一次生命周期。清理成功、清理失败、初始化失败后都不能再次注册，也不能移动到其他 Domain。

把缓存实例改为工厂：

```csharp
// v1.0：复用同一对象
domain.RegisterModel(cachedPlayer);

// v1.1：每次生命周期创建新对象
domain.RegisterModel(playerFactory.Create());
```

Utility 仍由调用方管理，同一实例可以在多个 Domain 共享。

## 4. 调整替换失败处理

替换采用 release-first：旧组件先从查找中移除并完成清理，新组件随后才初始化。任何阶段失败后主键都为空，旧组件不会恢复。

调用方不得假设捕获异常后仍能取得旧实例。需要恢复服务时，显式创建并注册一个新实例。若旧清理已经失败，原对象仍然不可复用；尚未开始初始化的新候选对象可以稍后重试。

## 5. 把不可回滚操作移出 Initialize

组件初始化期间可以查找、查询、注册本地事件和新增不存在的组件键，但不能发送命令或本地事件，也不能改变 Domain 关系或替换已有键。

把命令、事件发送和关系修改移到 Domain 完全活动后的启动流程。Utility 调用、全局事件、I/O 等外部副作用不属于框架事务；如果必须在初始化中执行，组件需要自行提供补偿逻辑。

## 6. 把 Disposed 当作终态

`UnInitialize()` 后不能再注册组件、发送事件、执行命令或查询。保留旧 Domain 引用只适合诊断性查找、读取 `Parent`、`ToString()` 或重复释放。

单例释放后再次访问 `Instance` 会得到新实例。独立 Domain 则应通过 `Create()` 或配置化工厂重新创建。

## 7. 更新清理回调

清理时当前组件已经从注册表移除，尚未清理的依赖项仍可读取。回调不能注册组件、发送事件/命令/查询或改变父子关系。

清理顺序为子 Domain、System、Model；同一分类按激活逆序。所有回调都必须支持部分初始化状态并允许其他清理阶段在自身失败后继续。

## 验证与回退

迁移后运行：

```powershell
dotnet test .\Test\Test.csproj
dotnet build .\SimpleFramework.sln --configuration Release
```

应使用仓库 `global.json` 指定的 .NET 9 SDK 或等价 CI 环境，并达到零警告、零错误。

若出现阻断性问题，受控消费者整体回退到 v1.0.0；修复后发布 v1.1.x。不要在同一进程中混用 v1.0 与 v1.1 生命周期语义。
