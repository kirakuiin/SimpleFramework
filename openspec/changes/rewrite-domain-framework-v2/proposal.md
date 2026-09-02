## Why

SimpleFramework 当前的 Domain 实现叠加了嵌套初始化事务、组件替换、双重父子语义、任意 `IDomain` 实现兼容和大量能力接口，已经偏离“轻量、易用”的目标。现在没有外部项目依赖，可以在不保留兼容层的前提下，融合 QFramework 的直观使用体验与现有 Domain 生命周期可靠性，建立一套更小且可验证的 v2 核心。

## What Changes

- **BREAKING** 重写 Domain 核心，只保留 Model、System、Utility 三类注册与获取，以及一种强引用父子树关系。
- **BREAKING** 将 `IDomain` 收窄为消费接口；注册、树修改和释放能力只公开在唯一受支持扩展点 `AbstractDomain`。
- **BREAKING** 将业务分类接口 `IModel`、`ISystem` 与 `IModelLifecycle`、`ISystemLifecycle` 分离，避免业务调用者直接操作生命周期。
- **BREAKING** 删除 `Require*`、`Register*As`、`SetParent`、`IController`、能力 Trait 森林、组件移除与替换、全局事件、公开 Domain 状态、公开 Children、任意第三方 `IDomain` 实现兼容以及异步 Command/Query。
- 使用“精确键、当前域唯一可赋值实例、父域递归”的分类解析规则，并为缺失、歧义、重复、阶段错误和失效对象提供确定异常契约。
- 恢复两阶段启动：`Configure` 先收集全部组件，再按 Utility、Model、System 顺序发布或初始化；Active 后只允许原子化新增全新键。
- 使用私有构造加显式静态工厂创建普通 Domain；使用无 `new()` 约束的严格单例 Domain，禁止同类型额外多例创建。
- 允许独立创建的 Active Domain 在运行期挂载、脱离和移动；父域回退只在成功挂载后生效，初始化不得依赖未来父域。
- 将同步 Command/Query Context 设计为不可逃逸的栈上值，将 Model/System Context 设计为受生命周期约束的窄能力对象。
- 事件采用本地同步、注册顺序、发送快照和 fail-fast 语义；热路径使用 copy-on-write 监听器数组避免发送分配。
- 固定后序、逆挂载、逆激活的确定释放顺序，并在失败时继续清理、维护树一致性和聚合异常。
- **BREAKING** 删除现有 Framework 测试与辅助类型，按 v2 契约从零建立完整黑盒测试套件，不保留旧行为断言。
- 为所有公共 API 编写完整中文 XML 注释，明确阶段、所有权、异常、线程和同步约束。

## Capabilities

### New Capabilities

- `domain-tree`: 定义独立创建、运行期挂载与脱离、父域回退生效时机、树级转换保护和子树释放语义。
- `domain-runtime-execution`: 定义同步 Command、Query、Context 与本地事件的权限、调度、异常、快照和热路径契约。

### Modified Capabilities

- `domain-lifecycle`: 用简化的创建、启动、Active、释放、严格单例和失败清理契约替换现有嵌套事务及可观察旧状态行为。
- `domain-component-registry`: 用稳定运行时键、显式契约键、延迟发布、无替换新增和分类所有权规则替换旧注册、替换与 `Register*As` 行为。

## Impact

- 主要影响根项目中的 `Framework.cs`、`AbstractDomain.cs`、`FrameworkExtension.cs`、Command/Query、Context、EventBus、抽象组件基类和单例 Domain 支持代码。
- `Test/Framework` 将整体重建；Toolkit、GDExt 及仓库内其他核心调用方需要适配新 API。
- Collections、ECS、Maths、Net 等无关模块不做顺手重构，只处理由核心 API 变化引起的必要编译调整。
- 这是原子化重大变更，不提供 `[Obsolete]` 转发、迁移适配层或旧行为兼容。
