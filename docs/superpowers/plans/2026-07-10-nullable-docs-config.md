# Nullable Docs Config Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Centralize shared project configuration, enable nullable reference types across the solution, document Domain lifecycle rules, and guard Chinese XML comments against corrupted text.

**Architecture:** Shared MSBuild defaults live in the root `Directory.Build.props`; project files keep only project-specific identity and references. Nullable fixes should express the existing runtime contract with accurate `?` annotations and narrow initialization suppressions. Documentation integrity is enforced by a focused NUnit test that scans production C# sources.

**Tech Stack:** C#/.NET 8, MSBuild, NUnit, PowerShell commands from repository root.

## Global Constraints

- Do not change public API behavior.
- Do not restructure modules or rename assemblies.
- Do not add runtime dependencies.
- Keep public API comments in Chinese where surrounding code uses Chinese XML docs.
- Prefer accurate nullable contracts over broad suppression.
- `dotnet build .\SimpleFramework.sln` must finish with 0 warnings and 0 errors.
- `dotnet test .\SimpleFramework.sln` must pass.

---

### Task 1: Centralize Project Configuration

**Files:**
- Create: `Directory.Build.props`
- Modify: `SimpleFramework.csproj`
- Modify: `Collections/Collections.csproj`
- Modify: `ECS/ECS.csproj`
- Modify: `GDExt/GDExt.csproj`
- Modify: `Maths/Maths.csproj`
- Modify: `Net/Net.csproj`
- Modify: `Patterns/Patterns.csproj`
- Modify: `Test/Test.csproj`
- Modify: `Toolkit/Toolkit.csproj`
- Modify: `Utility/Utility.csproj`

**Interfaces:**
- Produces: every project inherits `TargetFramework=net8.0`, `ImplicitUsings=enable`, `Nullable=enable`, and shared Debug/Release output paths from `Directory.Build.props`.

- [ ] **Step 1: Add shared MSBuild props**

Create `Directory.Build.props` with:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
    <OutputPath>$(MSBuildThisFileDirectory)bin\Debug\</OutputPath>
  </PropertyGroup>

  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <OutputPath>$(MSBuildThisFileDirectory)bin\Release\</OutputPath>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Remove duplicated shared properties**

In each project file, remove `TargetFramework`, `ImplicitUsings`, `Nullable`, and repeated Debug/Release `OutputPath` groups. Keep project-specific values such as `RootNamespace`, `AssemblyName`, `ProjectReference`, compile removes, and conditional package references.

- [ ] **Step 3: Verify expected nullable/build red state**

Run:

```powershell
dotnet build .\SimpleFramework.sln
```

Expected: build may fail or warn because the root framework project now participates in nullable analysis. Record the first warning/error categories for Task 2.

- [ ] **Step 4: Commit**

```powershell
git add Directory.Build.props *.csproj Collections\*.csproj ECS\*.csproj GDExt\*.csproj Maths\*.csproj Net\*.csproj Patterns\*.csproj Test\*.csproj Toolkit\*.csproj Utility\*.csproj
git commit -m "build: centralize project defaults"
```

### Task 2: Make Core Nullable Contracts Explicit

**Files:**
- Modify: `Framework.cs`
- Modify: `AbstractDomain.cs`
- Modify: `AbstractCommand.cs`
- Modify: `AbstractModel.cs`
- Modify: `AbstractQuery.cs`
- Modify: `AbstractSystem.cs`
- Modify: `FrameworkImpl/Container.cs`
- Modify as needed: root framework files that produce nullable warnings

**Interfaces:**
- Produces: optional lookups return nullable references: `GetSystem<T>()`, `GetModel<T>()`, `GetUtility<T>()`, and matching extension methods return `T?`.
- Produces: parent lookup returns `IDomain?`.
- Produces: `Require*` APIs continue to return non-null values and throw when missing.

- [ ] **Step 1: Run focused build**

Run:

```powershell
dotnet build .\SimpleFramework.csproj
```

Expected: nullable diagnostics identify root framework contracts that need annotation.

- [ ] **Step 2: Update interface contracts**

In `Framework.cs`, annotate nullable-returning APIs:

```csharp
IDomain? Parent { get; }
T? GetSystem<T>() where T : class, ISystem;
bool TryGetSystem<T>(out T? system) where T : class, ISystem;
T? GetModel<T>() where T : class, IModel;
bool TryGetModel<T>(out T? model) where T : class, IModel;
T? GetUtility<T>() where T : class, IUtility;
bool TryGetUtility<T>(out T? utility) where T : class, IUtility;
```

Keep `RequireSystem<T>()`, `RequireModel<T>()`, and `RequireUtility<T>()` non-null.

- [ ] **Step 3: Update core implementations**

Apply the same nullable contracts in `AbstractDomain.cs`, update `_domain`, `_parent`, `SetParent`, and local variables so null is explicit. Use `default!` or `null!` only for framework-managed initialization boundaries such as `Domain` properties set before execution.

- [ ] **Step 4: Update extension methods**

In `FrameworkExtension.cs`, make `GetSystem<T>`, `GetModel<T>`, and `GetUtility<T>` return `T?`. Keep `Require*` extensions non-null.

- [ ] **Step 5: Verify root framework build**

Run:

```powershell
dotnet build .\SimpleFramework.csproj
```

Expected: 0 warnings and 0 errors for the root framework project.

- [ ] **Step 6: Commit**

```powershell
git add Framework.cs AbstractDomain.cs AbstractCommand.cs AbstractModel.cs AbstractQuery.cs AbstractSystem.cs FrameworkExtension.cs FrameworkImpl\Container.cs
git commit -m "refactor: express framework nullable contracts"
```

### Task 3: Document Lifecycle Contracts and Guard Chinese Comments

**Files:**
- Create: `docs/domain-lifecycle.md`
- Create: `Test/Documentation/UnitTestSourceTextQuality.cs`
- Modify: any production `.cs` files flagged by the new source text quality test

**Interfaces:**
- Produces: `TestSourceTextQuality.TestProductionSourceDoesNotContainCorruptedChineseText()` NUnit test.
- Produces: `docs/domain-lifecycle.md` documenting Domain ownership, lookup, lifecycle, and event rules.

- [ ] **Step 1: Write failing source text quality test**

Create `Test/Documentation/UnitTestSourceTextQuality.cs`:

```csharp
using NUnit.Framework;

namespace SimpleFramework.Test.Documentation;

public class TestSourceTextQuality
{
    private static readonly string[] CorruptedTextMarkers =
    [
        "鑾", "瀵", "姹", "杩", "鍙", "涓", "褰", "棰", "缂", "瑙",
        "绾", "鐨", "灏", "鍦", "浣", "琛", "鐭", "榛", "瀛", "鏃",
        "鍒", "鍣", "娑", "伅", "璁", "綍", "銆", "锛", "€", "�"
    ];

    [Test]
    public void TestProductionSourceDoesNotContainCorruptedChineseText()
    {
        var root = GetRepositoryRoot();
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsUnder(path, root, "bin"))
            .Where(path => !IsUnder(path, root, "obj"))
            .Where(path => !IsUnder(path, root, "Test"))
            .ToArray();

        var failures = files
            .SelectMany(path => FindMarkers(path).Select(marker => $"{Path.GetRelativePath(root, path)} contains {marker}"))
            .ToArray();

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
    }

    private static IEnumerable<string> FindMarkers(string path)
    {
        var text = File.ReadAllText(path);
        return CorruptedTextMarkers.Where(text.Contains);
    }

    private static bool IsUnder(string path, string root, string directory)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SimpleFramework.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
```

- [ ] **Step 2: Verify the test fails if corrupted text exists**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~SourceTextQuality"
```

Expected: FAIL if existing production comments contain corrupted text markers; otherwise PASS and continue.

- [ ] **Step 3: Repair flagged Chinese XML comments**

For each flagged production file, replace corrupted XML comments with concise correct Chinese comments. Do not change implementation behavior.

- [ ] **Step 4: Add lifecycle documentation**

Create `docs/domain-lifecycle.md` covering:

```markdown
# Domain 生命周期契约

## 父子域

`AddChild` 表示生命周期所有权。父域释放时会释放子域，并保持对子域的强引用。

`SetParent` 只表示查找继承。当前域找不到 System、Model、Utility 时，会继续从父域查找。

## 组件生命周期

System 和 Model 注册后由 Domain 初始化和释放。重复注册同一实例不会重复初始化。

Utility 注册不纳入生命周期管理，即使运行时类型同时实现了 System 或 Model 接口。

## 查找语义

`Get*` 未找到时返回 `null`。`TryGet*` 用布尔值表达是否找到。`Require*` 未找到时抛出异常。

## 事件边界

Domain 事件只在当前 Domain 内生效，不沿父域查找。需要跨 Domain 通信时显式使用 `EventBus.Global`。

## 命令和查询

Command 代表可能修改状态的操作。Query 代表只读查询。Domain 在执行前会把自身注入到 Command 或 Query。
```

- [ ] **Step 5: Verify documentation test**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~SourceTextQuality"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add docs\domain-lifecycle.md Test\Documentation\UnitTestSourceTextQuality.cs
git add *.cs FrameworkImpl\*.cs ECS\*.cs Net\**\*.cs
git commit -m "docs: document domain lifecycle contracts"
```

### Task 4: Final Solution Verification

**Files:**
- Modify only files required to resolve final build/test issues.

**Interfaces:**
- Produces: solution builds and tests pass with nullable enabled everywhere.

- [ ] **Step 1: Run full build**

Run:

```powershell
dotnet build .\SimpleFramework.sln
```

Expected: 0 warnings and 0 errors. If nullable warnings remain, fix contracts or initialization boundaries without changing runtime behavior.

- [ ] **Step 2: Run full tests**

Run:

```powershell
dotnet test .\SimpleFramework.sln
```

Expected: all tests pass.

- [ ] **Step 3: Run diff check**

Run:

```powershell
git diff --check
```

Expected: no whitespace errors.

- [ ] **Step 4: Commit any final cleanup**

If files changed during this task:

```powershell
git add <changed-files>
git commit -m "chore: finish nullable documentation cleanup"
```

If no files changed, do not create an empty commit.
