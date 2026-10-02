## 1. 事件中心

- [x] 1.1 新增 `EventHub.cs`：`IEventHub : IUtility, IDisposable` 与默认实现 `EventHub`，内部复用 `EventBus`，释放后发布与订阅抛 `ObjectDisposedException`
- [x] 1.2 新增内部查找辅助：通过 `IDomain.TryGetUtility<IEventHub>` 获取 hub，找不到时抛带注册提示的 `KeyNotFoundException`

## 2. 语法糖

- [x] 2.1 `FrameworkExtension.cs`：`ICanSendEvent` 增加 `PublishEvent<T>(T)` 与无参 `PublishEvent<T>()`；`ICanRegisterEvent` 增加 `SubscribeEvent<T>(Action<T>)`
- [x] 2.2 `CommandContext` 增加 `PublishEvent<T>(T)` 与无参 `PublishEvent<T>()`
- [x] 2.3 `AbstractModel` 增加受保护的 `PublishEvent`；`AbstractSystem` 增加受保护的 `PublishEvent` 与 `SubscribeEvent`

## 3. 订阅随释放取消

- [x] 3.1 `AbstractSystem` 维护句柄列表，`SubscribeEvent` 自动登记；提前取消时从列表移除；`Release` 时先逆序取消全部句柄再调用 `OnRelease`
- [x] 3.2 新增扩展 `UnRegisterOnRelease(this IUnRegister, AbstractSystem)`，System 未绑定 Domain 时抛 `InvalidOperationException`

## 4. 创建时指定父域

- [x] 4.1 `CreateDomain` 增加可选 `parent`：校验 Active、启动期间仅建立查找链接、初始化后复核父域并挂载、释放时安全摘除
- [x] 4.2 补测试：初始化期间回退父域、父域仍在启动被拒、子域启动失败不影响父域、初始化中父域被释放导致启动失败

## 5. 审查修复

- [x] 5.1 释放期间登记订阅抛异常（新增释放中标记），`SubscribeEvent` 先校验再订阅，避免泄漏
- [x] 5.2 取消订阅逐个执行并收集异常，与 `OnRelease` 异常一并抛出；初始化失败时原始异常排首位；异常收集逻辑抽到 `FrameworkImpl/Failures.cs` 与 Domain 共用
- [x] 5.3 查找遇到已释放父域时报告“父 Domain 已释放”；`OnActivated` 期间被释放的报错说明可能是父域被释放
- [x] 5.4 更新 `RegisterEvent`、`OnRelease`、`ICanSendEvent`/`ICanRegisterEvent` 注释，`IEventHub` 注明非线程安全与释放归属
- [x] 5.5 补测试：释放期间订阅被拒、取消失败不泄漏且异常汇总、初始化失败异常顺序、父域场景的报错与列表移除
- [x] 5.6 `UnRegisterOnRelease` 被拒绝时先取消传入订阅；初始化失败清理期间同样拒绝登记；初始化失败改用 `Failures` 工具；补齐异常注释与规格场景

## 6. 测试与文档

- [x] 6.1 新增 `Test/Framework/EventHubTests.cs`：树内共享、子域遮蔽、Domain 释放不影响 hub、分发语义、hub 释放、Controller 与 Command、未注册提示、子域释放后订阅失效、`BindableProperty` 随释放取消、提前取消、热路径零分配
- [x] 6.2 `docs/domain-lifecycle.md` 新增跨 Domain 事件一节（含释放期间与本地事件的差异）；更新 `README.md` 核心章节与 `AGENTS.md` 核心行为说明
- [x] 6.3 全部类型与成员中文 XML 注释；运行 `dotnet build .\SimpleFramework.sln`、`dotnet test .\SimpleFramework.sln`、`openspec validate add-event-hub --strict`

## 7. 第四轮审查修复

- [x] 7.1 `RemoveChild` 同时要求子域为 Active，拒绝正在释放的子域提前摘除自身，补测试
- [x] 7.2 文档与注释说明 `UnRegisterOnRelease` 提前取消必须使用返回句柄，补测试固定返回句柄提前取消会移除登记项
- [x] 7.3 `DomainEventBus` 更名为 `EventBus`（同时服务 Domain 本地事件与 `EventHub`）；修正 docs 中释放后访问的异常描述
- [x] 7.4 docs 新增“释放子域”一节：直接 `Dispose` 子域即自动摘除，关闭通知中只清理引用、不调用 `RemoveChild`；释放规则与常见失败表补充对应条目
- [x] 7.5 docs“释放子域”补充：示例注明经事件中心在 `OnDeactivating` 中发布关闭通知；关闭通知中也不要移动正在释放子域的上级
