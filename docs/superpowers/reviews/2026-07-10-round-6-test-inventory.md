# Round 6 Test Simplification Inventory

本附录逐文件记录 Round 6 对全部 40 个 tracked test C# 文件的精简审查。判断单位是 setup → operation → observable contract；仅当三者相同且不损失边界、错误、顺序、并发或回归保护时才允许删除。

| 测试文件 / fixture | 主要 setup → operation → observable contract 族 | 潜在重复候选核对 | 决定与保留理由 |
| --- | --- | --- | --- |
| `Test/Collections/UnitTestCounter.cs` / `TestCounter` | comparer 配置 → 构造/更新/减法/关系运算 → 值、hash、右侧独有键、原子失败。 | `CrossComparerArithmetic`、`UpdateRejects...`、`SubtractRejects...` setup 相近；前者覆盖二元运算，后两者分别覆盖原地更新/减法不变性。 | 保留全部；右独有键、比较器兼容和失败前不变是不同回归边界。 |
| `Test/Collections/UnitTestDefaultDict.cs` / `TestDefaultDict` | factory/comparer → 索引、TryGet、pair contains/remove → 延迟创建及 comparer 语义。 | `Contains` 与 `PairContainsUsesConfiguredComparer` 表面重复；后者走 `ICollection<KeyValuePair<,>>` 且验证自定义 comparer。 | 保留全部；接口路径和缺失键不初始化边界不同。 |
| `Test/Documentation/UnitTestSourceTextQuality.cs` / `TestSourceTextQuality` | 仓库文本 → 扫描损坏中文模式 → 无匹配。 | 无同契约候选。 | 保留；唯一 source-quality 门禁。 |
| `Test/ECS/UnitTestArchetype.cs` / `UnitTestArchetype` | 对齐列/多行 archetype → Get/Set/boxed/swap-back → 组件行对齐、移动实体与拒绝事务性。 | 两个 `RemoveAtSwapBack` 分别覆盖中间行和末行；不能合并而丢失 moved/null 分支。 | 保留全部；引用写回、boxed 快照和失败不破坏存储是独立契约。 |
| `Test/ECS/UnitTestCommandBuffer.cs` / `UnitTestCommandBuffer` | world + buffered handles → record/playback/resolve → 顺序、批次身份、失败前缀和恢复。 | foreign buffer、same-local-id foreign result、stale handle 都使用相似句柄；身份来源和诊断分支不同。 | 保留全部；错误、顺序、消费批次和恢复均为回归路径。 |
| `Test/ECS/UnitTestComponent.cs` / `TestComponent` | struct/class component 类型 → 接口约束实例化 → 两类均合法。 | 无同契约候选。 | 保留；唯一覆盖引用/值组件类型边界。 |
| `Test/ECS/UnitTestEach.cs` / `UnitTestEach` | 查询实体 → 1/2 组件 ref 遍历及遍历中结构变更 → 可写回与 mutation guard。 | 一组件与两组件 Each setup 相似，但覆盖不同 overload/ref 参数个数。 | 保留全部；overload 和结构变更异常不可互代。 |
| `Test/ECS/UnitTestEcsSystem.cs` / `TestEcsSystem` | world/query/system → Update(delta/no-delta) → 匹配处理、fallback 与 null world 验证。 | 两个 Update 测试操作相近；一个验证实体处理，一个验证 delta overload fallback。 | 保留全部；入口和构造错误路径不同。 |
| `Test/ECS/UnitTestEntity.cs` / `UnitTestEntity` | 构造 handles → equality/string → WorldId/Id/Version 值身份。 | 无同契约候选。 | 保留；三个测试分别保护存储、相等性和诊断文本。 |
| `Test/ECS/UnitTestEntityPrefab.cs` / `UnitTestEntityPrefab` | prefab 组件集 → instantiate/拒绝重复或 null → 值复制、引用共享、失败不变。 | duplicate type、widened duplicate runtime type、同名不同 runtime type均相近；分别覆盖静态、运行时和程序集身份。 | 保留全部；类型身份边界不可合并。 |
| `Test/ECS/UnitTestQuery.cs` / `UnitTestQuery` | world/archetypes + include/exclude → lazy refresh/enumerate/clear → 动态匹配、缓存只读及 mutation guard。 | added excluded、removed included、destroyed entity 都导致结果移除，但触发结构变更不同。 | 保留全部；包含/排除/销毁三条更新路径和只读缓存均独立。 |
| `Test/ECS/UnitTestSignature.cs` / `TestTypeSignature` | component Type 集 → equality/Has/enumerate/string → 顺序无关、约束及程序集级类型区分。 | equality 与同名类型回归均比较 signature；后者专门防 comparer 混淆。 | 保留全部；类型身份回归不可由普通 equality 覆盖。 |
| `Test/ECS/UnitTestSystemGroup.cs` / `TestSystemGroup` | ordered/dependent systems → add/remove/validate/update → 稳定排序、cycle、disabled、reentrancy、异常传播。 | parameterless/delta、run-before/run-after、validate/update cycle 成对出现；分别覆盖不同入口和“验证不执行”边界。 | 保留全部；顺序、缓存失效、cycle-before-update 与 reentrant recovery 均不同。 |
| `Test/ECS/UnitTestWorld.cs` / `UnitTestWorld` | 多 world/entity/component → CRUD/结构迁移/query → 全局 ID、本地 slot、stale/foreign policy、swap-back。 | invalid/stale/foreign 操作矩阵 setup 重复；身份类别与期望 policy 不同。 | 保留全部；句柄世代、跨 world 和结构失败原子性是高价值边界。 |
| `Test/Extensions/UnitTestExtension.cs` / `TestString`, `TestEnumerable`, `TestList`, `TestRandom` | 小集合/随机源 → repeat/apply/swap/choice/shuffle/sample → 输出与 invalid count/empty 验证。 | Choice/Sample 都取随机元素；sample 数量与 choice 单值、空/负值失败不同。 | 保留全部；四个 extension 族和各自参数边界不重复。 |
| `Test/Framework/UnitTestFrame.cs` / `TestFramework` + lifecycle doubles | domain graph/registrations/events → register/replace/uninitialize/parent-child → lookup、所有权、顺序、回滚、重入。 | System/Model/Utility 注册测试结构高度对称；三者生命周期管理规则明确不同。事件 clear/unregister 与 parent/child case 也保护不同 token/边界。 | 保留全部；该文件是 Core 生命周期事务、角色交叉和事件局部性的主回归账本。 |
| `Test/Maths/UnitTestHexagonGrid.cs` / `TestHexagonGrid` | hex/layout/orientation → 算术、round、line、坐标转换 → 几何结果、overflow/non-finite/zero scale。 | 多个 non-finite/overflow 拒绝测试输入相似；分别落在 Hex、FractionalHex、Layout、Orientation 和 reciprocal 分支。 | 保留全部；异常参数名和数值域边界不同。 |
| `Test/Maths/UnitTestMathUtils.cs` / `TestMathUtils` | 固定角度 → radians/degrees 转换 → 双向常数结果。 | 两测试互为逆向但调用不同 API。 | 保留；每个 public conversion 各一最小契约。 |
| `Test/Maths/UnitTestMatrix2D.cs` / `TestMatrix2D` | scale/general matrix → create/dot/inverse → 数值结果及 near-singular/non-finite 拒绝。 | 三个 inverse 失败测试相近；分别覆盖 determinant 阈值、输入非有限和候选结果非有限。 | 保留全部；不同数值失败分支。 |
| `Test/Net/NetCoreTests.cs` / `NetCoreTests` | GameNet options/transport/discovery → validate/stop/dispose/concurrent host → structured status、目录/诊断清理、重启。 | 多个 after-dispose 测试 setup 相同；分别覆盖 connection/message/stats-flow/request/discovery public facade。Stop 与 Dispose 清理入口也不同。 | 保留全部；public entry-point 矩阵及 stop/dispose 状态机不能用单一烟测替代。 |
| `Test/Net/NetDiscoveryStatsTests.cs` / `NetDiscoveryStatsTests` | memory/UDP/blocking backends + manual time → advertise/scan/browser/stats → filter/schema/envelope、取消、并发、事件、RTT。 | invalid packet cases参数化但 envelope、endpoint、payload、decode 各触发不同诊断；browser/discovery dispose 分别拥有不同任务。 | 保留全部；新增真实 scan overload 调用回归替代反射测试，保留 65 个 discovery/stats 边界。 |
| `Test/Net/NetFlowTests.cs` / `NetFlowTests` | 多 peer session → propose/respond/resend/stop/disconnect → policy 结果、pending snapshot、timeout/cancel/queue failure。 | All/Any/Majority/Quorum setup相似但接受阈值不同；timeout、cancel、disconnect 都结束/保持 pending 的规则不同。 | 保留全部；策略、生命周期和传输失败状态不可合并。 |
| `Test/Net/NetMessagingTests.cs` / `NetMessagingTests` + attributed handlers | joined peers/registry/codec/queues → send/broadcast/relay/request/assembly scan → 顺序、限额、rollback、fingerprint、pending cleanup。 | sync/async transport throw、packet/byte/rate queue limit、request timeout/cancel/unexpected response 看似成族；每个对应不同 reservation/cleanup 分支。 | 保留全部；消息注册事务、handler ordering、请求身份和错误隔离均是独立回归。 |
| `Test/Net/NetSessionTests.cs` / `NetSessionTests` + gated transports | host/join/auth/reconnect setup → join/leave/kick/stop/reconnect races → peer directory、事件顺序、rollback、grace token。 | stop/dispose/join/reconnect race 大量共用 gates；阻塞点分别位于 send/auth/accept/reconnect expiry，observable state 不同。 | 保留全部；竞态测试不可因 setup 重复删除。 |
| `Test/Net/NetTransportTests.cs` / `NetTransportTests` | memory/TCP endpoints + failure writers/time → start/connect/send/stop/dispose → ordering、frame integrity、status、race cleanup。 | Memory/TCP 都测 invalid/cancel/missing/dispose，但实现与资源所有权不同；TCP frame failure/header/oversize 分支亦不同。 | 保留全部；跨 transport contract 对称测试是必要一致性证据。 |
| `Test/Net/TestDoubles/ManualTimeProvider.cs` / `ManualTimeProvider` | 排序 timer 队列 → Advance/Fire/Dispose → 确定性时间推进与 timer 生命周期。 | 非测试 fixture；无删除候选。 | 保留；被 timeout、重连、浏览器和 stats 测试共享，移除会迫使 sleep-based 测试。 |
| `Test/Patterns/UnitTestBlackBoard.cs` / `TestBlackBoard` | parent/local board + handlers/threads → set/get/remove/clear/hierarchy/events → null shadow、snapshot、锁外重入。 | hierarchy Get/TryGet/Contains 和 local override 共享数据；lookup 形状与 null/遮蔽语义不同。 | 保留全部；线程安全、事件注销和层级本地写边界均独立。 |
| `Test/Patterns/UnitTestMessageChannel.cs` / `TestMessageChannel` | normal/buffered channels + nested handlers → publish/subscribe/dispose/replay → pending mutation、异常隔离、token ownership。 | normal/buffered dispose 与 subscribe 测试相似；buffer replay、last message 和失败回滚是额外状态。 | 保留全部；reentrant publish、stale/collected token 和 replay failure 是回归边界。 |
| `Test/Patterns/UnitTestObjectPool.cs` / `TestObjectPool` | callbacks/listener pool → get/return/prewarm/clear/dispose → reuse、计数、duplicate/null、callback retry。 | configured callback 与 listener callback failure 都测 retry；调用源和失败路径不同。 | 保留全部；对象身份、重复归还和失败后可重试不可互代。 |
| `Test/Patterns/UnitTestServiceLocator.cs` / `TestServiceLocator` | registered base/derived services → register/get/unregister → exact/assignable lookup、duplicate replacement、missing/null。 | multiple/derived service setup相近；一个测多 key，一个测 assignable fallback。 | 保留全部；定位优先级和失败语义不同。 |
| `Test/Patterns/UnitTestSingleton.cs` / singleton doubles | concurrent/failing initializer → Instance/Destroy → 单次发布、销毁重建、失败不缓存。 | Use/ConcurrentFirstAccess 都观察实例，但并发只保护 publication race。 | 保留全部；初始化失败回归和并发边界独立。 |
| `Test/Patterns/UnitTestStateMachine.cs` / `TestStateMachine` | root/nested states + callback faults → setup/activate/dispatch/update/deactivate → ownership、消费、顺序、fail-closed/rollback。 | setup/enter/update/exit failure tests结构相似；失败阶段、已发布状态和递归恢复范围不同。AnyState/handler/transition 也保护不同优先级。 | 保留全部；生命周期阶段和层级事务不可安全合并。 |
| `Test/Toolkit/UnitTestIniConfigTool.cs` / `TestIniConfigTool` | 临时 INI + cultures/auto-flush → load/get/set/save/reload/dispose → roundtrip、default、comments、conversion、持久化。 | basic/save-reload/reload 都触及 I/O；分别验证首轮解析、跨实例持久化和内存替换。 | 保留全部；culture、auto-flush、dispose-save 和 invalid conversion 是独立边界。 |
| `Test/Utility/UnitTestDisposable.cs` / `TestDisposable` | disposable group + throwing/null children → add/dispose/repeat → 幂等、全量释放、聚合异常、late add。 | group/custom idempotence 均重复 dispose；一个验证基类，一个验证组合所有权。 | 保留全部；throwing child 后继续和 disposed 后 add 是错误路径。 |
| `Test/Utility/UnitTestFileUtil.cs` / `TestFileUtil` | 临时文件 + simple/complex data → JSON/binary save/load/overwrite → roundtrip、missing false、warning、serialization failure。 | JSON 与 binary roundtrip setup相同但编码/I/O路径不同；complex/overwrite 观察也不同。 | 保留全部；两种格式和错误诊断不可互代。 |
| `Test/Utility/UnitTestLogging.cs` / `TestLogging` + handlers | logger handlers/formatter/levels + blocking/throwing dispose → emit/remove/clear → snapshot dispatch、锁外释放、聚合失败。 | basic/custom/levels/formatter 是不同配置面；clear/remove 都 dispose 但批量等待与单项传播不同。 | 保留全部；并发 emit 与 disposal lock ordering 是回归保护。 |
| `Test/Utility/UnitTestMiscUtil.cs` / nested hash/name fixtures | 多种 Type/data → unique name/GUID/type hash/compute hash → 稳定、分布、缓存、UTF-8 bytes。 | hash consistency/stability/cache 看似重复；分别观察同进程重复、固定期望和缓存行为。 | 保留全部；类型身份、数据 hash 与性能 smoke 各自独立。 |
| `Test/Utility/UnitTestSerializeUtil.cs` / `TestSerializeUtil` | string/bytes/runtime/collections → serialize/deserialize → roundtrip、nullable result、无中间 string allocation。 | string/binary/runtime 路径输入相似但 API 和分配契约不同。 | 保留全部；格式/泛型入口与 allocation 回归不可合并。 |
| `Test/Utility/UnitTestTaskUtil.cs` / `TestTaskUtil` | predicate + manual timing/cancel → WaitUntil → already-true、timeout、并发、delay cap、active cancellation、参数错误。 | timeout/cancel 都提前结束；一个时间耗尽，一个 token 中断 active delay。 | 保留全部；同步 predicate throw、并发和剩余超时 cap 是不同分支。 |
| `Test/Utility/UnitTestTimeUtil.cs` / `TestTimeUtil` | current/known DateTime → UTC milliseconds/timespan/ToMs → 合理范围、固定值、overflow。 | 两个 now tests 都取 UTC；一个检查实时范围，一个检查已知输入转换。 | 保留全部；overflow 是独立错误边界。 |

## 结论与计数校验

- 40/40 tracked test C# 文件均有逐文件决定；删除 0 个测试。
- 没有任何候选同时满足相同 setup、相同 operation、相同 observable contract 且不损失边界/错误/顺序/并发/回归保护。
- 最终仍为 40 个文件、752 个 VSTest 测试；Round 6 仅保留一个 discovery public API 回归测试的净新增。

```powershell
$tracked = git ls-files 'Test/*.cs' | Sort-Object
$ledger = Select-String -Path '.\docs\superpowers\reviews\2026-07-10-round-6-test-inventory.md' -Pattern '^\| `(?<path>Test/[^`]+\.cs)` /' |
    ForEach-Object { $_.Matches[0].Groups['path'].Value } | Sort-Object
if ($tracked.Count -ne 40 -or $ledger.Count -ne 40) { throw "tracked=$($tracked.Count), ledger=$($ledger.Count)" }
if (Compare-Object $tracked $ledger) { throw 'test inventory paths differ' }
```
