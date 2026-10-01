## 1. 公共接口

- [x] 1.1 在 `Framework.cs` 中新增 `IBelongToDomain` 与七个 `ICanXxx` 规则接口，以及 `IController`
- [x] 1.2 在 `FrameworkExtension.cs` 中为每个规则接口提供扩展方法（只有 `Get` 系列，无 `TryGet`），保留 `DomainExtensions`，删除 `ModelLookupExtensions` / `SystemLookupExtensions`
- [x] 1.3 将 `IModelLifecycle` / `ISystemLifecycle` 改为 `Initialize(IDomain)` / `Release()` 并继承本角色规则接口；删除 `IModelContext` / `ISystemContext`
- [x] 1.4 `CommandContext` / `QueryContext` 改为持有 `IDomain`、公开构造函数、移除 `TryGet`

## 2. 内部实现

- [x] 2.1 精简 `FrameworkImpl/DomainComponentRegistry.cs`：按分类的有序注册与字典查找，保留按实例去重和歧义检测；删除 `LifecycleOwnershipTracker` 与资源归属
- [x] 2.2 删除 `FrameworkImpl/ComponentContext.cs`、`FrameworkImpl/DomainTreeState.cs`
- [x] 2.3 `FrameworkImpl/Event.cs`：订阅失效时置空回调，`Trigger` 跳过已失效订阅；`DomainEventBus` 释放时失效全部订阅

## 3. Domain 与基类

- [x] 3.1 重写 `AbstractDomain`：阶段规则、Configure 内注册、Model→System 初始化与失败回滚、执行期可改树与释放、释放顺序与异常汇总、日志
- [x] 3.2 简化 `AbstractSingletonDomain<T>` 为 `_instance` + `_isCreating`
- [x] 3.3 重写 `AbstractModel` / `AbstractSystem`：显式实现生命周期与 `GetDomain`，提供 `protected` 能力方法，重复初始化抛异常，Release 后清空绑定
- [x] 3.4 所有类型、成员、枚举字段补齐中文 XML 注释；复杂逻辑注释说明原因；函数内逻辑块空行分隔

## 4. 测试

- [x] 4.1 重写 `Test/Framework/DomainV2Fixtures.cs` 适配新 API
- [x] 4.2 改写注册与查找测试：精确/可赋值/歧义/父级、缺失抛异常、`IDomain.TryGet`、重复注册、Configure 外注册
- [x] 4.3 改写生命周期测试：初始化顺序、System 初始化期间取后注册的 System、失败回滚、释放顺序与异常汇总、释放期间查找、重入 Dispose、单例
- [x] 4.4 新增执行与事件测试：命令中挂载/释放、事件处理中释放后续处理器不再调用、分发中取消订阅、释放后访问抛 `ObjectDisposedException`、`IController`、直接构造 `CommandContext`
- [x] 4.5 改写日志测试，删除仅验证已移除守卫或 `DisposeSelfOnly` 的用例
- [x] 4.6 运行 `dotnet build .\SimpleFramework.sln` 与 `dotnet test .\SimpleFramework.sln` 全部通过

## 5. 文档与收尾

- [x] 5.1 重写 `docs/domain-lifecycle.md`，更新 `README.md` 核心章节与 `AGENTS.md` 核心行为说明
- [x] 5.2 运行 `openspec validate simplify-domain-framework`，确认核心 `.cs` 行数约 900～1000
- [x] 5.3 询问用户是否进行独立代码审查

## 6. 审查修复

- [x] 6.1 释放时先进入 Disposing 再释放子树，修复子域回调重入释放父域、修改父域子域列表的问题，并补测试
- [x] 6.2 恢复“只能启动一次”检查，工厂误返回已在使用的实例时不触发清理，并补测试
- [x] 6.3 `OnDeactivating` 只在进入过 Active 时调用，并补测试
- [x] 6.4 文档写明非本地订阅需在 `OnRelease` 中取消、建议按业务接口获取组件、`AggregateException` 展开行为
- [x] 6.5 补齐注册方法、`Parent`、`AddChild`/`RemoveChild` 的 `<exception>` 注释；`CommandContext` 增加无参 `SendCommand<T>()` / `SendEvent<T>()`
- [x] 6.6 补测试：释放后 Model 能力失效、可赋值查找与 Query 零分配；全部测试方法补充测试内容注释

## 7. 复审修复

- [x] 7.1 事件失效提前到进入 Disposing 时，使子树释放阶段父域也拒绝订阅、不分发事件，并补测试
- [x] 7.2 “只能启动一次”改用独立布尔标记，覆盖 Configure 中再次启动同一实例，并补测试
- [x] 7.3 修正 `OnDeactivating` 条件注释与非本地订阅泄漏的文档措辞；补测试：`OnActivated` 抛异常时仍调用 `OnDeactivating`
- [x] 7.4 design 风险清单记录“直接释放子域时回调释放父域”的已知限制
- [x] 7.5 补充 `Disposing` 状态、`IDomain.SendEvent`/`RegisterEvent` 注释，spec 增加 Configure 重入启动场景，测试断言重入启动后实例已释放
- [x] 7.6 文档与 `OnRelease` 注释写明组件逆序释放、不要在 `OnRelease` 中调用其他 System，协调逻辑放在 `OnDeactivating`，并补测试；基类“未绑定 Domain”异常信息说明可能已释放
