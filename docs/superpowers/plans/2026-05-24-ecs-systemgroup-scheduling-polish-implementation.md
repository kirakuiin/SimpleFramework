# ECS SystemGroup Scheduling Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add cached sorting, explicit validation, enabled-state skipping, and ECS usage documentation for `SystemGroup`.

**Architecture:** `SystemGroup` owns a cached sorted entry list invalidated by membership changes. `Validate()` and both update methods share the same cache-building path. `EcsSystem.Enabled` is a lightweight execution gate; disabled systems stay in the dependency graph but are skipped when dispatching updates.

**Tech Stack:** C# 12 / .NET 8, NUnit, existing `SimpleFramework.ECS` project.

---

### Task 1: Add EcsSystem Enabled

**Files:**
- Modify: `ECS/System.cs`
- Modify: `Test/ECS/UnitTestSystemGroup.cs`

- [ ] **Step 1: Write failing tests**

Add tests asserting a disabled system is skipped by `Update()` and by `Update(float)`.

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter TestSystemGroup
```

Expected: build fails because `EcsSystem.Enabled` does not exist.

- [ ] **Step 3: Implement minimal Enabled support**

Add `public bool Enabled { get; set; } = true;` to `EcsSystem`. In `SystemGroup.UpdateCore`, skip entries where `entry.System.Enabled` is false.

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter TestSystemGroup
```

Expected: PASS.

### Task 2: Add Validate and Cache

**Files:**
- Modify: `ECS/SystemGroup.cs`
- Modify: `Test/ECS/UnitTestSystemGroup.cs`

- [ ] **Step 1: Write failing tests**

Add tests proving `Validate()` detects cycles without running systems, `Update()` can use a validated cached order, and `Add()` / `Remove()` invalidate that cache.

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter TestSystemGroup
```

Expected: build fails because `SystemGroup.Validate()` does not exist.

- [ ] **Step 3: Implement cache and Validate**

Add cached sorted entries to `SystemGroup`. `Add()` and `Remove()` clear the cache. `Validate()` calls the same cache-building path as update and does not execute systems.

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter TestSystemGroup
```

Expected: PASS.

### Task 3: Pin Disabled Dependency Semantics

**Files:**
- Modify: `Test/ECS/UnitTestSystemGroup.cs`
- Modify: `ECS/SystemGroup.cs`

- [ ] **Step 1: Write failing tests**

Add tests showing disabled systems still participate in cycle detection and do not reorder other systems when skipped.

- [ ] **Step 2: Run tests**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter TestSystemGroup
```

Expected: any missing disabled dependency behavior fails.

- [ ] **Step 3: Adjust implementation if needed**

Keep disabled systems in sorted entries and only skip them during update dispatch.

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter TestSystemGroup
```

Expected: PASS.

### Task 4: Add ECS Usage Documentation

**Files:**
- Create: `ECS/USAGE.md`

- [ ] **Step 1: Write documentation**

Create an example-first document for `World`, `Query`, `SystemGroup`, dependencies, `Validate()`, and `Enabled`.

- [ ] **Step 2: Check documentation references**

Run:

```powershell
rg -n "Validate|Enabled|RunBefore|RunAfter|Query" ECS/USAGE.md
```

Expected: all key concepts are present.

### Task 5: Full Verification

**Files:**
- Check: `ECS/System.cs`
- Check: `ECS/SystemGroup.cs`
- Check: `ECS/USAGE.md`
- Check: `Test/ECS/UnitTestSystemGroup.cs`

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
