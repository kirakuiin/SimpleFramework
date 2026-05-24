# ECS SystemGroup Dependency Sort Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `RunBefore` and `RunAfter` dependency declarations to `SystemGroup` with stable topological sorting and cycle diagnostics.

**Architecture:** Dependencies are declared as attributes on `EcsSystem` types. `SystemGroup` keeps the existing manual `order` and insertion `sequence` baseline, builds dependency edges inside each manual-order phase, and uses stable Kahn sorting before each update. Cycle detection throws before any system update runs and reports a concrete type-name chain.

**Tech Stack:** C# 12 / .NET 8, NUnit, existing `SimpleFramework.ECS` project.

---

### Task 1: Add Dependency Attributes

**Files:**
- Create: `ECS/SystemDependencyAttributes.cs`
- Test: `Test/ECS/UnitTestSystemGroup.cs`

- [ ] **Step 1: Write the failing test**

Add this test to `Test/ECS/UnitTestSystemGroup.cs`:

```csharp
[Test]
public void DependencyAttributesThrowWhenSystemTypeIsNull()
{
    Assert.Throws<ArgumentNullException>(() => new RunBeforeAttribute(null));
    Assert.Throws<ArgumentNullException>(() => new RunAfterAttribute(null));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter DependencyAttributesThrowWhenSystemTypeIsNull
```

Expected: build fails because `RunBeforeAttribute` and `RunAfterAttribute` do not exist.

- [ ] **Step 3: Write minimal implementation**

Create `ECS/SystemDependencyAttributes.cs`:

```csharp
namespace SimpleFramework.ECS;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RunBeforeAttribute : Attribute
{
    public RunBeforeAttribute(Type systemType)
    {
        SystemType = systemType ?? throw new ArgumentNullException(nameof(systemType));
    }

    public Type SystemType { get; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RunAfterAttribute : Attribute
{
    public RunAfterAttribute(Type systemType)
    {
        SystemType = systemType ?? throw new ArgumentNullException(nameof(systemType));
    }

    public Type SystemType { get; }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter DependencyAttributesThrowWhenSystemTypeIsNull
```

Expected: PASS.

### Task 2: Sort RunAfter Dependencies

**Files:**
- Modify: `ECS/SystemGroup.cs`
- Modify: `Test/ECS/UnitTestSystemGroup.cs`

- [ ] **Step 1: Write the failing test**

Add this test and nested system class to `Test/ECS/UnitTestSystemGroup.cs`:

```csharp
[Test]
public void UpdateRunsRunAfterSystemAfterReferencedSystem()
{
    var world = new World();
    var calls = new List<string>();
    var group = new SystemGroup();

    group.Add(new AfterFirstSystem(world, calls, "after"));
    group.Add(new FirstDependencySystem(world, calls, "first"));

    group.Update();

    CollectionAssert.AreEqual(new[] { "first", "after" }, calls);
}

[RunAfter(typeof(FirstDependencySystem))]
private sealed class AfterFirstSystem : RecordingSystem
{
    public AfterFirstSystem(World world, List<string> calls, string name) : base(world, calls, name)
    {
    }
}

private sealed class FirstDependencySystem : RecordingSystem
{
    public FirstDependencySystem(World world, List<string> calls, string name) : base(world, calls, name)
    {
    }
}
```

Change the existing `RecordingSystem` declaration from `private sealed class RecordingSystem` to `private class RecordingSystem` so dependency-specific test systems can inherit it.

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter UpdateRunsRunAfterSystemAfterReferencedSystem
```

Expected: FAIL because the current sort keeps insertion order and runs `after` before `first`.

- [ ] **Step 3: Write minimal implementation**

In `ECS/SystemGroup.cs`, change `GetSortedEntries()` from simple LINQ ordering to a stable dependency sort:

```csharp
private List<Entry> GetSortedEntries()
{
    var baseline = _entries
        .OrderBy(entry => entry.Order)
        .ThenBy(entry => entry.Sequence)
        .ToList();

    return StableTopologicalSort(baseline);
}
```

Add helpers that build same-order dependency edges and run Kahn sorting.

- [ ] **Step 4: Run test to verify it passes**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter UpdateRunsRunAfterSystemAfterReferencedSystem
```

Expected: PASS.

### Task 3: Sort RunBefore Dependencies and Preserve Stability

**Files:**
- Modify: `Test/ECS/UnitTestSystemGroup.cs`
- Modify: `ECS/SystemGroup.cs`

- [ ] **Step 1: Write failing tests**

Add tests for `RunBefore`, missing dependencies, stable unrelated systems, cross-order dependencies, and delta-time order. The assertions should use real `SystemGroup.Update()` and `SystemGroup.Update(float)` calls.

- [ ] **Step 2: Run tests to verify they fail where behavior is missing**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter TestSystemGroup
```

Expected: `RunBefore` and any incomplete dependency behavior fail before implementation.

- [ ] **Step 3: Complete sorting implementation**

Add `RunBeforeAttribute` handling, missing-dependency filtering, assignable-type matching, duplicate-edge suppression, and same-order-only dependency filtering.

- [ ] **Step 4: Run tests to verify they pass**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter TestSystemGroup
```

Expected: PASS.

### Task 4: Detect Cycles With Dependency Chains

**Files:**
- Modify: `Test/ECS/UnitTestSystemGroup.cs`
- Modify: `ECS/SystemGroup.cs`

- [ ] **Step 1: Write failing cycle tests**

Add tests that create a cycle using dependency attributes, assert `InvalidOperationException`, assert the message contains the full chain, and assert no system update method ran.

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter TestSystemGroup
```

Expected: cycle tests fail because no cycle diagnostic exists yet.

- [ ] **Step 3: Implement cycle detection**

After Kahn sorting, if the sorted count is smaller than the baseline count, find a cycle with DFS through remaining graph edges and throw:

```text
SystemGroup dependency cycle detected: ASystem -> BSystem -> ASystem
```

- [ ] **Step 4: Run tests to verify they pass**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter TestSystemGroup
```

Expected: PASS.

### Task 5: Full Verification

**Files:**
- Check: `ECS/SystemDependencyAttributes.cs`
- Check: `ECS/SystemGroup.cs`
- Check: `Test/ECS/UnitTestSystemGroup.cs`
- Check: `docs/superpowers/specs/2026-05-24-ecs-systemgroup-dependency-sort-design.md`
- Check: `docs/superpowers/plans/2026-05-24-ecs-systemgroup-dependency-sort-implementation.md`

- [ ] **Step 1: Run full test suite**

Run:

```powershell
dotnet test .\SimpleFramework.sln --no-restore -m:1 /nr:false
```

Expected: all tests pass.

- [ ] **Step 2: Run ECS mojibake scan**

Run:

```powershell
rg -n "瀛|鎸|绯|涓嶅|銆|€" ECS
```

Expected: no matches.

- [ ] **Step 3: Review diff**

Run:

```powershell
git diff -- ECS Test docs/superpowers/specs/2026-05-24-ecs-systemgroup-dependency-sort-design.md docs/superpowers/plans/2026-05-24-ecs-systemgroup-dependency-sort-implementation.md
```

Expected: only planned files changed.
