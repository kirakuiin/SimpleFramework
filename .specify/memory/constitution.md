<!--
Sync Impact Report:
- Version change: [INITIAL] → 1.0.0
- Modified principles: N/A (initial creation)
- Added sections: All sections (initial creation)
- Removed sections: N/A
- Templates requiring updates:
  ✅ .specify/templates/plan-template.md (updated - Constitution Check, Technical Context, Project Structure)
  ✅ .specify/templates/tasks-template.md (updated - Test paths, C# naming conventions, Agent references)
  ✅ .specify/templates/spec-template.md (no changes needed - already generic)
- Follow-up TODOs: None
-->

# SimpleFramework 项目准则

## 核心原则

### I. 模块化架构 (NON-NEGOTIABLE)

SimpleFramework 采用严格的模块化设计，每个模块必须：

- **单一职责**：每个模块处理一个明确的关注点（如 ECS、Net、Patterns 等）
- **清晰边界**：模块间通过明确的接口通信，避免直接依赖内部实现
- **独立可测**：每个模块必须能够独立进行单元测试
- **最小依赖**：模块间依赖关系必须保持最小化，优先依赖抽象而非具体实现

**理由**：模块化是框架的核心价值，确保代码可维护、可扩展、易于理解。

### II. SOLID 原则

所有代码设计必须严格遵循 SOLID 原则：

- **单一职责原则 (SRP)**：每个类/方法只负责一个功能
- **开闭原则 (OCP)**：对扩展开放，对修改关闭
- **里氏代换原则 (LSP)**：子类必须能够替换父类
- **接口隔离原则 (ISP)**：接口应该细小且专注，客户端不应依赖不需要的接口
- **依赖倒转原则 (DIP)**：依赖抽象而非具体实现

**理由**：SOLID 原则是软件工程的基础，确保代码的可维护性和可扩展性。

### III. 测试驱动开发 (NON-NEGOTIABLE)

测试是代码质量的生命线，必须遵守：

- **测试框架**：使用 NUnit 框架
- **命名规范**：测试用例采用 `Test` 开头的驼峰命名（如 `TestAbcAbc`）
- **测试先行**：重要功能必须先编写测试用例
- **覆盖率要求**：核心模块必须有充分的单元测试覆盖
- **测试分类**：区分单元测试和集成测试

**理由**：测试确保功能正确性，防止回归，支持安全重构。

### IV. 代码审查 (NON-NEGOTIABLE)

所有代码变更必须经过审查流程：

- **生成后审查**：代码生成完成后必须通知 review agent 进行审查
- **审查内容**：逻辑正确性、代码风格、设计思路、性能考虑
- **审查通过**：只有通过审查的代码才能合并到主分支
- **QA 验证**：审查通过后，由 qa agent 生成测试用例进行验证

**理由**：代码审查是保证代码质量和团队知识共享的关键环节。

### V. DRY 与 KISS 原则

- **DRY (Don't Repeat Yourself)**：避免代码重复，提取公共逻辑到可复用组件
- **KISS (Keep It Simple, Stupid)**：保持简单，避免过度设计

**理由**：简洁的代码更易于理解、维护和调试。

### VI. 事件驱动架构

SimpleFramework 的核心是事件驱动模式：

- **域内事件**：Domain 内部支持事件传递和处理
- **事件总线**：提供全局事件总线机制
- **事件注册**：支持事件的注册与注销
- **属性绑定**：支持带通知的属性绑定

**理由**：事件驱动架构实现模块间的松耦合，提供更好的扩展性。

### VII. 命令查询分离 (CQRS)

严格分离命令和查询操作：

- **Command**：用于修改数据的操作，不应有返回值（或仅返回操作结果）
- **Query**：用于只读数据的查询，不应修改数据

**理由**：CQRS 模式提高代码的可维护性和性能优化的灵活性。

## 开发约束

### 技术栈要求

- **语言**：C#
- **编码格式**：UTF-8
- **交互语言**：使用中文与开发者进行交互
- **框架版本**：遵循项目当前目标框架版本

### 代码风格

- 遵循现有项目的代码风格
- 使用一致的命名约定（PascalCase 用于公开成员，camelCase 用于私有成员）
- 保持适当的代码注释和文档

### 架构模式

- **DDD 领域驱动设计**：Domain、System、Model、Utility 分层
- **ECS 实体组件系统**：轻量级 ECS 框架实现
- **设计模式**：合理使用单例、服务定位器、消息通道等模式

## 开发工作流

### 代码生成流程

1. **需求分析**：理解开发者需求，列出思路和主要步骤
2. **讨论确认**：与开发者讨论方案，得到确认后再生成代码
3. **代码实现**：按照确认的方案生成代码
4. **代码审查**：通知 review agent 审查代码
5. **测试验证**：通知 qa agent 生成测试用例并执行测试
6. **Git 提交**：使用 git-commit-operator agent 进行提交

### 提交规范

- 提交信息使用中文
- 提交信息清晰描述变更内容
- 遵循项目的 Git 工作流

### Agent 协作

项目使用三个专门的子代理辅助开发：

- **review agent**：专业的代码审查人员（`.claude/agents/review.md`）
- **qa agent**：专业的测试人员（`.claude/agents/qa.md`）
- **git agent**：Git 操作员（`.claude/agents/git.md`）

## 治理规则

### 准则优先级

本准则凌驾于所有其他实践之上。当其他实践与本准则冲突时，以本准则为准。

### 修订流程

- 准则的修订必须经过文档化、团队讨论和批准
- 修订必须包含迁移计划，确保现有代码的兼容性
- 所有修订必须更新版本号并记录修订日期

### 版本控制

采用语义化版本控制 (MAJOR.MINOR.PATCH)：

- **MAJOR**：重大变更，向后不兼容的原则删除或重新定义
- **MINOR**：新增原则或大幅扩展现有原则
- **PATCH**：澄清、措辞、错别字修正等非语义性改进

### 合规性审查

- 所有 Pull Request 和代码审查必须验证是否符合本准则
- 引入复杂度必须有明确的理由和文档说明
- 运行时开发参考 `CLAUDE.md` 文件

### 指导文件

- **CLAUDE.md**：AI 助手的运行时指导文件，包含项目概览和交互方式
- **README.md**：项目的整体介绍和使用说明

**版本**: 1.0.0 | **批准日期**: 2026-02-26 | **最后修订**: 2026-02-26
