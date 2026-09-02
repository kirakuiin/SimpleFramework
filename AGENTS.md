# AGENTS.md

本文件供在此仓库中工作的编码智能体使用。`README.md` 面向开发者；本文件记录项目背景、常用命令和本地约定。

## 项目概述

SimpleFramework 是一个由多个项目组成的 C#/.NET 解决方案，用于构建轻量级游戏及应用基础设施。项目目标框架为 `net8.0`；`global.json` 将 SDK 固定为 `9.0.0`，并设置 `rollForward: latestMinor`。

主要项目：

- `SimpleFramework.csproj`：`Domain v2` 核心框架，支持分类组件查找、动态 `Domain` 树、生命周期 `Context`、同步栈上下文命令/查询抽象、本地事件、可绑定属性以及可选的抽象组件基类。
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

## 核心行为说明

- 具体 `Domain` 使用私有构造函数，并通过调用 `CreateDomain(() => new Domain(...))` 的静态工厂创建；严格单例继承 `AbstractSingletonDomain<T>`，通过 `GetOrCreateInstance` 暴露 `Instance`。
- `IDomain` 仅用于消费。注册、`AddChild`/`RemoveChild`、`Dispose` 和 `DisposeSelfOnly` 通过 `AbstractDomain` 提供。
- 子 `Domain` 在执行 `AddChild` 前已经独立处于 `Active` 状态。唯一的强树关系同时提供父级组件回退和默认子树释放；`RemoveChild` 永远不会释放子 `Domain`。
- 未显式指定泛型契约的 `Register` 重载以运行时具体类型作为唯一键。查找顺序为：本地精确匹配、本地唯一可赋值匹配、父级匹配；存在歧义时抛出异常。
- `Model`/`System` 生命周期一旦开始初始化，便具有全局排他性且只执行一次。`Active` 阶段的注册只能添加不存在的键；不支持替换、删除，也不支持初始化期间的嵌套注册。
- `Utility` 由调用方拥有，可以在多个 `Domain` 之间共享。运行时对象若跨越多个组件类别，将被拒绝注册。
- `Model`/`System` 的 `Context` 能力受生命周期阶段限制，在 `OnDeactivating`/`Release` 之前无效。`Command`/`Query` 使用同步的 `readonly ref struct` `Context` 值。
- `Domain` 事件为本地事件并采用写时复制；不存在全局事件总线。跨 `Domain` 通信应使用显式共享的 `Utility` 或服务。
- `BindableProperty<T>.WithComparer` 仅作用于当前实例。

## 模块说明

- ECS 实体 ID 是静态的，并在所有 `World` 之间单调递增。
- `Query` 不持有事件订阅或释放生命周期；它根据 `World` 的 `Archetype` 版本延迟刷新缓存的 `Archetype` 匹配结果。
- `BlackBoard` 使用读写锁并支持父级查找；本地写入不会修改父级 BlackBoard。
- Godot 代码由 `#if GODOT` 保护；普通 .NET 构建仍可能通过解决方案还原 `GDExt` 依赖。
