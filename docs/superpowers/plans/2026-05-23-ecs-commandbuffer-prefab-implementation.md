# ECS CommandBuffer Prefab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Strengthen ECS boundary coverage and add first-pass CommandBuffer, EntityPrefab, and Each traversal sugar.

**Architecture:** Keep `World` as the only executor of entity lifecycle and structural changes. `CommandBuffer` records ordered operations and replays through `World`; `EntityPrefab` stores default components for one entity; `Each` wraps `Query` plus `Get<T>` without changing traversal safety rules.

**Tech Stack:** C#/.NET 8, NUnit, existing `SimpleFramework.ECS` project.

---

### Task 1: ECS Boundary Tests

**Files:**
- Modify: `Test/ECS/UnitTestWorld.cs`
- Modify: `Test/ECS/UnitTestQuery.cs`

- [ ] **Step 1: Add failing tests**

Add tests for component migration preserving existing values, excluded query removal after adding an exclude component, include query removal after removing an include component, `Set<T>` updating without archetype migration, invalid handle policy, and duplicate component type creation.

- [ ] **Step 2: Run focused tests**

Run: `dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter "FullyQualifiedName~Test.ECS"`
Expected: duplicate component tests fail before implementation; existing behavior tests document current expectations.

- [ ] **Step 3: Implement minimal world validation**

Make `World.CreateEntityWithComponents` reject duplicate component runtime types before building the component dictionary. Preserve current invalid entity behavior: `Has/TryGet/DestroyEntity` return false; `Get/Add/Set/Remove` throw.

- [ ] **Step 4: Run focused tests**

Run: `dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter "FullyQualifiedName~Test.ECS"`
Expected: PASS.

### Task 2: CommandBuffer

**Files:**
- Create: `ECS/CommandBuffer.cs`
- Create: `Test/ECS/UnitTestCommandBuffer.cs`

- [ ] **Step 1: Add failing tests**

Cover ordered playback for create/add/set/remove/destroy, buffered entity resolution, query enumeration with buffered structural changes, and invalid entity propagation.

- [ ] **Step 2: Implement API**

Add `BufferedEntity`, `CommandBufferResult`, and `CommandBuffer`. Commands should replay in record order and delegate behavior to `World`.

- [ ] **Step 3: Run focused tests**

Run: `dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter "FullyQualifiedName~Test.ECS.UnitTestCommandBuffer"`
Expected: PASS.

### Task 3: EntityPrefab

**Files:**
- Create: `ECS/EntityPrefab.cs`
- Create: `Test/ECS/UnitTestEntityPrefab.cs`
- Modify: `ECS/World.cs`

- [ ] **Step 1: Add failing tests**

Cover fluent prefab creation, instantiation with default values, duplicate component rejection, and class component reference semantics.

- [ ] **Step 2: Implement API**

Add `EntityPrefab.Create()`, `With<T>`, internal component snapshot access, and `World.Instantiate(EntityPrefab prefab)`.

- [ ] **Step 3: Run focused tests**

Run: `dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter "FullyQualifiedName~Test.ECS.UnitTestEntityPrefab"`
Expected: PASS.

### Task 4: Each Sugar

**Files:**
- Create or Modify: `ECS/ECSExtension.cs`
- Create: `Test/ECS/UnitTestEach.cs`

- [ ] **Step 1: Add failing tests**

Cover `Each<T1>` and `Each<T1,T2>` mutating components by ref and throwing when structural changes happen inside traversal.

- [ ] **Step 2: Implement API**

Add ref delegates and `World.Each<T1>` / `World.Each<T1,T2>` partial methods that iterate matching queries.

- [ ] **Step 3: Run focused tests**

Run: `dotnet test .\Test\Test.csproj --no-restore -m:1 /nr:false --filter "FullyQualifiedName~Test.ECS.UnitTestEach"`
Expected: PASS.

### Task 5: Comments, Encoding, and Full Verification

**Files:**
- Modify: `ECS/Archetype.cs`
- Modify: `ECS/Query.cs`
- Modify: `docs/superpowers/specs/2026-05-23-ecs-commandbuffer-prefab-design.md`

- [ ] **Step 1: Repair Chinese XML comments**

Replace mojibake in touched ECS public API comments with readable Chinese.

- [ ] **Step 2: Run mojibake scan**

Run: `rg -n "瀛|鎸|绯|涓嶅|銆|€" ECS`
Expected: no matches.

- [ ] **Step 3: Run full fixed test command**

Run: `dotnet test .\SimpleFramework.sln --no-restore -m:1 /nr:false`
Expected: PASS.

---

## Self-Review

Spec coverage: boundary tests, CommandBuffer, Prefab, Each, and encoding scan are covered. `SystemGroup` is intentionally out of scope for this implementation plan.

Placeholder scan: no open placeholders remain.

Type consistency: planned public names match the approved API shape and current ECS naming.
