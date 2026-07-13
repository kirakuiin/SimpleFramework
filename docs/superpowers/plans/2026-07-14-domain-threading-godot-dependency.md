# Domain Threading Contract and Godot Dependency Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Document Domain's single-thread ownership contract and remove Net's unused Godot package references without changing runtime behavior.

**Architecture:** Keep `AbstractDomain<T>` implementation unchanged and express the threading boundary consistently in API XML, the root usage guide, and the lifecycle contract. Keep all Godot compilation inside GDExt by removing only Net's conditional package references.

**Tech Stack:** C#/.NET 8, MSBuild project files, Markdown, NUnit

## Global Constraints

- Domain remains a single-thread object with no new locks or runtime thread checks.
- The same thread, normally the game or application main thread, creates, accesses, and releases a Domain.
- Callers marshal background work back to the Domain-owning thread before calling Domain APIs.
- GDExt keeps its `GODOT` compile symbol, GodotSharp 4.5.1 reference, and `#if GODOT` source guards.
- Do not modify the user's existing change in `docs/superpowers/reviews/2026-07-10-round-7-release.md`.

---

### Task 1: Document the Domain threading contract

**Files:**
- Modify: `AbstractDomain.cs:5-44`
- Modify: `README.md:21-31`
- Modify: `docs/domain-lifecycle.md:1-3`
- Test: `Test/Documentation/UnitTestSourceTextQuality.cs`

**Interfaces:**
- Consumes: Existing `AbstractDomain<T>.Instance`, `Create()`, and `UnInitialize()` behavior.
- Produces: A public contract stating that all Domain operations use one caller-owned thread.

- [ ] **Step 1: Confirm the contract is absent before editing**

Run:

```powershell
rg -n "不是线程安全|所属线程|主线程" AbstractDomain.cs README.md docs\domain-lifecycle.md
```

Expected: no Domain threading-contract match; the command may exit with code 1.

- [ ] **Step 2: Add the API XML contract**

Extend the `AbstractDomain<T>` class documentation with:

```csharp
/// <remarks>
/// Domain 不是线程安全的。实例应由同一线程（通常是游戏或应用主线程）创建、访问和释放；
/// 调用方负责在调用 Domain API 前将后台工作调度回该线程。
/// </remarks>
```

Extend the `Instance` documentation with:

```csharp
/// <remarks>首次创建和后续访问都必须发生在 Domain 所属线程。</remarks>
```

Extend the `Create()` documentation with:

```csharp
/// <remarks>返回的实例应始终由创建它的线程访问和释放。</remarks>
```

- [ ] **Step 3: Add the user-facing usage guidance**

After the Core usage example in `README.md`, add:

```markdown
`Domain` 不是线程安全的，应由同一线程（通常是游戏或应用主线程）创建、访问和释放。后台任务可以执行独立计算或 I/O，但在注册组件、发送事件、执行命令/查询或释放 Domain 前，应由应用自己的调度机制回到 Domain 所属线程。
```

At the beginning of `docs/domain-lifecycle.md`, add:

```markdown
## 线程模型

`Domain` 是单线程对象，不提供并发同步。创建、组件访问、父子关系、事件、命令、查询和 `UnInitialize()` 应在同一线程执行；该线程通常是游戏或应用主线程。后台任务完成后，调用方应先调度回 Domain 所属线程，再调用 Domain API。
```

- [ ] **Step 4: Verify documentation quality**

Run:

```powershell
dotnet test .\Test\Test.csproj --configuration Debug --filter "FullyQualifiedName~TestSourceTextQuality"
rg -n "不是线程安全|所属线程|主线程" AbstractDomain.cs README.md docs\domain-lifecycle.md
```

Expected: source-text test passes 1/1 and all three public documentation layers contain the threading contract.

- [ ] **Step 5: Commit the contract documentation**

```powershell
git add AbstractDomain.cs README.md docs\domain-lifecycle.md
git commit -m "docs: define domain threading contract"
```

### Task 2: Remove Net's unused Godot dependencies

**Files:**
- Modify: `Net/Net.csproj:13-16`
- Verify unchanged: `GDExt/GDExt.csproj`

**Interfaces:**
- Consumes: GDExt's direct `GodotSharp` 4.5.1 package reference and `GODOT` compile symbol.
- Produces: A Net project with no Godot package dependency and unchanged public API.

- [ ] **Step 1: Record the dependency baseline**

Run:

```powershell
rg -n "GodotSharp|GODOT" Net\Net.csproj GDExt\GDExt.csproj
```

Expected: Net contains the conditional 4.2.0 references and GDExt contains its compile symbol and 4.5.1 reference.

- [ ] **Step 2: Delete only Net's conditional ItemGroup**

Remove this complete block from `Net/Net.csproj`:

```xml
<ItemGroup Condition="'$(GODOT)' == 'true'">
    <PackageReference Include="GodotSharp" Version="4.2.0" />
    <PackageReference Include="GodotSharpEditor" Version="4.2.0" Condition="'$(Configuration)' == 'Debug'" />
</ItemGroup>
```

Do not modify `GDExt/GDExt.csproj` or either guarded GDExt source file.

- [ ] **Step 3: Restore and verify the dependency graph**

Run:

```powershell
dotnet restore .\SimpleFramework.sln
rg -n "GodotSharp|GODOT" Net\Net.csproj GDExt\GDExt.csproj
```

Expected: restore succeeds; Net has no matches; GDExt still defines `GODOT` and references GodotSharp 4.5.1.

- [ ] **Step 4: Verify the Godot extension project**

Run:

```powershell
dotnet build .\GDExt\GDExt.csproj --configuration Release --no-restore
```

Expected: GDExt builds with 0 warnings and 0 errors.

- [ ] **Step 5: Commit the project cleanup**

```powershell
git add Net\Net.csproj
git commit -m "build: remove unused net godot dependencies"
```

### Task 3: Run final repository verification

**Files:**
- Verify: `SimpleFramework.sln`
- Verify unchanged: `GDExt/GDExt.csproj`, `GDExt/Utils.cs`, `GDExt/Extensions.cs`

**Interfaces:**
- Consumes: Completed documentation and project cleanup from Tasks 1 and 2.
- Produces: Fresh evidence that all projects and tests remain healthy.

- [ ] **Step 1: Run the full Debug test suite**

Run:

```powershell
dotnet test .\SimpleFramework.sln --configuration Debug --no-restore
```

Expected: all 763 tests pass with 0 failures and 0 skips.

- [ ] **Step 2: Run the full Release build**

Run:

```powershell
dotnet build .\SimpleFramework.sln --configuration Release --no-restore
```

Expected: all solution projects, including GDExt, build with 0 warnings and 0 errors.

- [ ] **Step 3: Audit the final diff and workspace**

Run:

```powershell
git diff --check HEAD~2..HEAD
git status --short
git diff -- GDExt\GDExt.csproj GDExt\Utils.cs GDExt\Extensions.cs docs\superpowers\reviews\2026-07-10-round-7-release.md
```

Expected: no whitespace errors; GDExt files have no diff; the only remaining workspace change is the user's pre-existing review-record modification.
