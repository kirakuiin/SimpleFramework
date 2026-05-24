# ECS SystemGroup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add deterministic `SystemGroup` updates and delta-time support to `EcsSystem`.

**Architecture:** `EcsSystem` remains the base contract and gains a virtual `Update(float deltaTime)` fallback that calls `Update()`. `SystemGroup` is a focused collection type that stores systems with explicit order and insertion sequence, then updates a sorted snapshot while rejecting group mutation during update.

**Tech Stack:** C# 12 / .NET 8, NUnit, existing `SimpleFramework.ECS` project.

---

### Task 1: Add EcsSystem Delta-Time Fallback

**Files:**
- Modify: `ECS/System.cs`
- Modify: `Test/ECS/UnitTestEcsSystem.cs`

- [x] **Step 1: Write the failing test**

Add this test to `Test/ECS/UnitTestEcsSystem.cs`:

```csharp
[Test]
public void DeltaTimeUpdateFallsBackToParameterlessUpdate()
{
    var world = new World();
    var system = new CountingSystem(world);

    system.Update(0.25f);

    Assert.AreEqual(1, system.UpdateCount);
}
```

Add this helper class inside `TestEcsSystem`:

```csharp
private sealed class CountingSystem : EcsSystem
{
    public CountingSystem(World world) : base(world)
    {
    }

    public int UpdateCount { get; private set; }

    public override void Update()
    {
        UpdateCount++;
    }
}
```

- [x] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter DeltaTimeUpdateFallsBackToParameterlessUpdate
```

Expected: build fails because `EcsSystem.Update(float)` does not exist.

- [x] **Step 3: Write minimal implementation**

Add this method to `ECS/System.cs`:

```csharp
public virtual void Update(float deltaTime)
{
    Update();
}
```

- [x] **Step 4: Run test to verify it passes**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter DeltaTimeUpdateFallsBackToParameterlessUpdate
```

Expected: PASS.

### Task 2: Add SystemGroup

**Files:**
- Create: `ECS/SystemGroup.cs`
- Create: `Test/ECS/UnitTestSystemGroup.cs`

- [x] **Step 1: Write failing tests**

Create `Test/ECS/UnitTestSystemGroup.cs` with tests for add/update, order sorting, stable same-order sorting, delta-time update, remove, duplicate add, and mutation during update.

- [x] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter TestSystemGroup
```

Expected: build fails because `SystemGroup` does not exist.

- [x] **Step 3: Write minimal implementation**

Create `ECS/SystemGroup.cs` as a sealed class with `Add`, `Remove`, `Update()`, and `Update(float deltaTime)`. Store entries with system instance, order, and insertion index. Reject null arguments, duplicate instances, and Add/Remove while updating.

- [x] **Step 4: Run SystemGroup tests**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter TestSystemGroup
```

Expected: PASS.

### Task 3: Full Verification

**Files:**
- Check: `ECS/System.cs`
- Check: `ECS/SystemGroup.cs`
- Check: `Test/ECS/UnitTestEcsSystem.cs`
- Check: `Test/ECS/UnitTestSystemGroup.cs`

- [x] **Step 1: Run full test suite**

Run:

```powershell
dotnet test .\SimpleFramework.sln --no-restore -m:1 /nr:false
```

Expected: all tests pass.

- [x] **Step 2: Run ECS mojibake scan**

Run:

```powershell
rg -n "瀛|鎸|绯|涓嶅|銆|€" ECS
```

Expected: no matches.

- [x] **Step 3: Review git diff**

Run:

```powershell
git diff -- ECS Test docs/superpowers/plans/2026-05-24-ecs-systemgroup-implementation.md
```

Expected: only planned files changed.
