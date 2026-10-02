# AGENTS.md

本文件供在此仓库中工作的编码智能体使用。`README.md` 面向开发者；本文件记录项目背景、常用命令和本地约定。

## 项目概述

SimpleFramework 是一个由多个项目组成的 C#/.NET 解决方案，用于构建轻量级游戏及应用基础设施。项目目标框架为 `net8.0`；`global.json` 将 SDK 固定为 `9.0.0`，并设置 `rollForward: latestMinor`。

主要项目：

- `SimpleFramework.csproj`：QFramework 风格的 Domain 核心框架，支持分类组件查找、动态 `Domain` 树、`ICanXxx` 能力规则接口、`IController`、同步栈上下文命令/查询、本地事件、跨 Domain 事件中心 `IEventHub`、可绑定属性以及 `AbstractModel`/`AbstractSystem` 基类。
- `Collections/Collections.csproj`：提供 `DefaultDict<TKey,TValue>` 和 `Counter<T>`。
- `ECS/ECS.csproj`：强调可读性的 ECS 实现，包含 `World`、`Entity`、`Archetype`、`TypeSignature`、`Query`、`IComponent` 和 `EcsSystem`。
- `Patterns/Patterns.csproj`：提供单例、服务定位器、对象池、消息通道、黑板以及事件驱动的分层状态机。
- `Maths/Maths.csproj`：提供点与数学辅助功能、矩阵和六边形网格工具。
- `Utility/Utility.csproj`：提供释放、日志、序列化、文件、时间、任务等辅助功能，以及集合、字符串和随机数扩展。
- `Net/Net.csproj`：提供 `GameNet` 编排、传输抽象、会话与对等端目录、强类型消息及编解码器、局域网发现、多人游戏流程、统计和诊断功能。
- `Toolkit/Toolkit.csproj`：提供实现 `IUtility` 的 INI 配置读写器 `IniConfigTool`。
- `GDExt/GDExt.csproj`：提供 Godot 节点辅助功能和池化扩展。项目定义了 `GODOT`，引用 `GodotSharp`，并使用 `#if GODOT` 保护源文件。
- `Test/Test.csproj`：主要模块的 NUnit 测试。

## 常用命令

在仓库根目录运行：

```powershell
dotnet restore .\SimpleFramework.sln
dotnet build .\SimpleFramework.sln
dotnet test .\SimpleFramework.sln
dotnet test .\Test\Test.csproj
```

## 编码约定

- 沿用现有命名空间和项目边界。除非任务明确要求，否则不要在项目之间移动源文件。
- 根核心项目通过 `Compile Remove` 有意排除了子模块目录；新增代码应放入正确的子项目，不要依赖根项目编译。
- `Directory.Build.props` 为所有项目启用了可空引用类型；代码应与现有可空性标注保持一致。
- 不要编辑生成的 `bin/` 或 `obj/` 文件。
- 优先使用符合现有代码风格的小而直接的 API。项目重视可读性而非激进优化，ECS 模块尤其如此。
- 当现有协议或序列化代码有此要求时，使用 `Utility/SerializeUtil.cs` 中的 `System.Text.Json` 辅助方法。

## 编码智能体指导纲领

> **最高原则：正确性、兼容性和可验证性优先；选择满足当前需求的最简单设计，不为假想的未来需求增加复杂度。**

- 在确实提高可读性、降低复杂度时，优先使用项目支持的现代 C# 语法；不以新颖或代码更短为目标。
- 模块、类型和函数各自只承担一个连贯的职责与变化原因；相关数据和行为放在一起，缩小公开 API，并让可变状态只有明确的所有者和修改入口。
- 保持封装和依赖方向清晰：面向稳定契约，优先组合；只有存在真实边界、已知变化或隔离价值时才引入接口、继承、模式或依赖注入，不为每个实现机械创建抽象。
- 消除同一稳定知识的重复；若抽象会掩盖意图或不同概念可能独立演化，允许少量重复。
- 命名应表达领域意图并保持术语一致；副作用、空值、错误、资源所有权及生命周期必须显式，查询不应隐藏修改。
- 所有类型、枚举和接口都使用中文 XML 文档注释说明用途；公共 API 说明重要约束，复杂逻辑的注释解释“为什么”、边界和风险，不复述代码。
- 修改应小步且限于当前需求；优先用测试验证行为，否则使用构建、静态检查或明确的运行时验证清单，并如实记录未验证部分。

## 独立代码审查

- 每批源代码（含测试）编写并完成基本验证后、最终交付前，询问用户是否使用 [`$review-agent`](C:/Users/wangzhuowei/.codex/skills/.system/review-agent/SKILL.md) 独立审查；纯文档修改不触发，用户已明确要求审查时不重复询问，每批最多主动询问一次。
- 用户同意后，创建 `gpt-5.6-terra`、`high` 推理等级、`fork_turns: none` 的子智能体，不继承主会话；向其提供用户意图、验收目标、设计与关键取舍、项目约束、审查范围、验证结果和已知风险。
- 审查者必须读取并遵循 `$review-agent`，基于实际完整差异和相关调用点进行只读审查，不修改代码、不提交、不再次委派；若技能不可用，应明确报告，不以普通审查替代。
- 除缺陷审查外，任务简报必须明确要求审查者对照当前项目 `AGENTS.md` 的“编码智能体指导纲领”检查本次改动，单独说明规范符合性并给出可执行建议；规范建议与 `$review-agent` 的缺陷发现分开呈现。
- 主智能体收到审查结果后，必须结合用户意图、设计、实际代码和验证证据逐项复核，不盲从审查结论；向用户反馈完整结果，说明问题是否成立、依据、优先级及处理建议。是否修复及修复后是否再次审查由用户决定，不自动形成递归审查。

## Domain 设计底线

修改 Domain 核心框架前先阅读 `docs/domain-lifecycle.md` 的“设计目标与原则”一节。要点：

- 目标是“顺手”：用法向 QFramework 看齐但更规范，复杂度尽量低；健壮性可以为易用让步，但整体必须强于 QFramework。
- 底线：用户没有犯错时绝不出问题；用户犯错时抛出明确的 .NET 标准异常，不静默覆盖、不返回 `null`。不新增自定义异常类型，Domain 不实现终结器。
- 查找、发送本地事件、发布到事件中心、执行 Command/Query 等热路径热身后不分配内存，修改时保持相关零分配测试通过。
- 不要为了“更健壮”重新引入已删除的机制：执行期树守卫、全局生命周期所有权表、Context 三态、注册两阶段、`DisposeSelfOnly`、Active 阶段注册、按需初始化、静态全局事件总线。新增防护必须针对上述底线中的具体问题。

## 核心行为说明

- 具体 `Domain` 使用私有构造函数，并通过调用 `CreateDomain(() => new Domain(...))` 的静态工厂创建；严格单例继承 `AbstractSingletonDomain<T>`，通过 `GetOrCreateInstance` 暴露 `Instance`。
- `IDomain` 仅用于消费。注册方法是 `AbstractDomain` 的 `protected` 成员，只能在 `Configure` 中调用；`AddChild`/`RemoveChild`、`Dispose` 由 `AbstractDomain` 提供。
- 能力由 `ICanXxx` 规则接口在编译期约束，与 QFramework 一致：Model 只能获取 Utility、发送事件；System 不能发送 Command/Query；Query 不能获取 Utility。`AbstractModel`/`AbstractSystem` 以 `protected` 方法提供能力，`IController` 等纯接口实现者使用扩展方法；`TryGet*` 只在 `IDomain` 上提供。
- 未显式指定泛型契约的 `Register` 重载以运行时具体类型作为唯一键。查找顺序为：本地精确匹配、本地唯一可赋值匹配、父级匹配；存在歧义时抛出异常。注册即可被查找，初始化期间可能拿到尚未初始化的组件。
- 启动先初始化全部 Model 再初始化全部 System；失败时逆序释放已初始化的组件并释放 Domain。不支持替换、删除组件或 Active 阶段注册。
- 子 `Domain` 可以通过 `CreateDomain(factory, parent)` 创建时指定 Active 父域：初始化期间即可回退查找父域组件，启动成功后自动挂载，启动失败不影响父域；也可以先独立创建到 `Active` 再 `AddChild`。回退查找遇到已释放的父域抛 `ObjectDisposedException`。唯一的强树关系同时提供父级组件回退和默认子树释放；`RemoveChild` 永远不会释放子 `Domain`。挂载、移除、释放可以在 Command、Query、事件处理中进行，已释放 Domain 的访问抛 `ObjectDisposedException`。
- 释放顺序：进入 Disposing 并失效全部本地事件订阅 → 子树逆序 → `OnDeactivating`（仅进入过 Active 时）→ System 逆序 → Model 逆序 → 清空注册表。释放期间仍可读取组件，但不能订阅本地事件或修改树；重入 `Dispose` 无操作。事件中心订阅不会在 Domain 进入 Disposing 时统一失效；`AbstractSystem` 自有订阅在各 System 释放时、`OnRelease` 之前取消，因此 `OnDeactivating` 期间仍可能收到事件中心通知。组件按初始化逆序释放，`OnRelease` 中不要调用其他 System；需要协调的退出逻辑放在 `OnDeactivating`。
- 跨 Domain 事件使用调用方持有的 `IEventHub`（默认实现 `EventHub`），通常注册在根 Domain；`PublishEvent` 跟随 `ICanSendEvent`，`SubscribeEvent` 跟随 `ICanRegisterEvent`，Query 两者都没有。`AbstractSystem.SubscribeEvent` 与 `UnRegisterOnRelease(this)` 登记的订阅在 System 释放（`OnRelease` 之前）或初始化失败时取消；释放期间登记会抛异常。Controller 的订阅由自己取消。
- 只有 Domain 本地事件与 System 自有订阅会自动取消；订阅父域 `BindableProperty` 等非本地来源时使用 `UnRegisterOnRelease(this)`。
- `Utility` 由调用方拥有，可以在多个 `Domain` 之间共享。运行时对象若跨越多个组件类别，将被拒绝注册。
- `Command`/`Query` 使用同步的 `readonly ref struct` 上下文，构造函数公开以便测试。
- `Domain` 事件为本地事件并采用写时复制；分发中被取消或所属 Domain 已释放的订阅会被跳过。不存在静态全局事件总线，跨 `Domain` 通信使用 `IEventHub` 或其他显式共享的 `Utility`。
- `BindableProperty<T>.WithComparer` 仅作用于当前实例。

## 模块说明

- ECS 实体 ID 是静态的，并在所有 `World` 之间单调递增。
- `Query` 不持有事件订阅或释放生命周期；它根据 `World` 的 `Archetype` 版本延迟刷新缓存的 `Archetype` 匹配结果。
- `BlackBoard` 使用读写锁并支持父级查找；本地写入不会修改父级 BlackBoard。
- Godot 代码由 `#if GODOT` 保护；普通 .NET 构建仍可能通过解决方案还原 `GDExt` 依赖。
