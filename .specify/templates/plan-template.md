# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: C# [目标框架版本或 NEEDS CLARIFICATION]
**Primary Dependencies**: [具体依赖包或 NEEDS CLARIFICATION]
**Storage**: [如适用，如文件系统、内存或其他，或 N/A]
**Testing**: NUnit（Test 开头的驼峰命名）
**Target Platform**: [如 .NET 版本、操作系统等或 NEEDS CLARIFICATION]
**Project Type**: library/framework (模块化 C# 框架)
**Performance Goals**: [领域特定的性能目标或 NEEDS CLARIFICATION]
**Constraints**: [领域特定的约束，如内存使用、响应时间等或 NEEDS CLARIFICATION]
**Scale/Scope**: [领域特定的规模范围或 NEEDS CLARIFICATION]

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [ ] **模块化架构**: 功能是否属于单一模块？是否有清晰的模块边界？
- [ ] **SOLID 原则**: 设计是否符合单一职责、开闭原则等 SOLID 原则？
- [ ] **事件驱动**: 是否合理使用事件系统实现模块间通信？
- [ ] **CQRS**: 命令和查询是否分离？
- [ ] **测试覆盖**: 是否有明确的测试计划（NUnit 框架）？
- [ ] **代码审查**: 准备好 review agent 和 qa agent 的审查流程

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
# SimpleFramework 模块化结构
SimpleFramework/          # 核心框架
├── Domain/               # 领域驱动设计基础
├── Models/               # 数据模型
├── Systems/              # 业务逻辑系统
└── Utilities/            # 工具类

ECS/                     # 实体组件系统
Net/                     # 网络模块
Patterns/                # 设计模式实现
Collections/             # 扩展集合
Maths/                   # 数学工具
Utility/                 # 通用工具
Test/                    # 所有模块的单元测试 (NUnit)
├── Test[ClassName].cs   # 测试文件（驼峰命名）
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
