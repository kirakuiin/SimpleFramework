# Domain 线程契约与 Godot 依赖清理设计

## 背景

`AbstractDomain<T>` 的单例创建、组件容器、父子关系、事件总线和生命周期状态均未提供并发同步，但当前公共文档没有明确说明线程模型。与此同时，`Net/Net.csproj` 在 `GODOT=true` 时引用 GodotSharp 4.2.0，而 Net 源码不使用 Godot API；真正的 Godot 集成由 `GDExt` 项目负责，并独立引用 GodotSharp 4.5.1。

## 目标

- 明确 Domain 是单线程对象，应由同一线程（通常是游戏或应用主线程）创建、访问和释放。
- 明确调用方负责把后台线程工作调度回 Domain 所属线程。
- 不为 Domain 增加锁、线程检查或其他运行时行为。
- 删除 Net 项目未使用且版本不一致的 Godot 条件依赖。
- 保持 GDExt 的条件编译和现有 Godot 功能不变。

## 设计

### Domain 线程契约

在以下三层文档中表达同一契约：

1. `AbstractDomain<T>` 类 XML 文档说明整个实例不是线程安全的。
2. `Instance` XML 文档说明首次创建与后续访问都必须发生在 Domain 所属线程；`Create()` 创建的独立 Domain 同样遵守该约束。
3. 根 `README.md` 和 `docs/domain-lifecycle.md` 说明典型用法是由主线程持有 Domain，后台任务只执行独立计算或 I/O，完成后由应用自己的调度机制回到主线程再调用 Domain API。

该契约覆盖组件注册与查找、父子关系、事件、命令、查询以及 `UnInitialize()`。文档不承诺自动检测错误线程，也不规定具体游戏引擎的调度 API。

### Godot 依赖清理

从 `Net/Net.csproj` 删除条件为 `$(GODOT) == true` 的两个 GodotSharp 包引用。保留：

- `GDExt/GDExt.csproj` 的 `GODOT` 编译符号；
- GDExt 对 GodotSharp 4.5.1 的直接引用；
- GDExt 源码中的 `#if GODOT`；
- GDExt 对 Net 和核心项目的项目引用。

因此普通 .NET、Net 和 GDExt 的公共 API 与运行逻辑均不变化。唯一不再支持的是未记录的用法：直接以 `GODOT=true` 构建 Net，并依赖 Net 间接提供 GodotSharp 包。

## 验证

- 运行 `dotnet restore .\SimpleFramework.sln`，确认删除条件依赖后项目图可正常还原。
- 运行 `dotnet test .\SimpleFramework.sln --configuration Debug`，确认全部现有测试通过。
- 运行 `dotnet build .\SimpleFramework.sln --configuration Release --no-restore`，确认包括 GDExt 在内的全部项目以零错误构建。
- 检查最终差异，确认没有修改 Domain 运行实现、GDExt 条件编译或用户已有的审查记录改动。

## 非目标

- 不使 Domain 线程安全。
- 不增加线程亲和性运行时检查。
- 不升级 GodotSharp 或测试依赖版本。
- 不调整 Net、GDExt 的公共 API 或项目边界。
