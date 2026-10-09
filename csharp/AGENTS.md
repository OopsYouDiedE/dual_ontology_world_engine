# Core 维护规范

本目录使用 .NET 8、C# 12、nullable reference types 和 implicit usings。世界引擎原理详见 [统一设计规范](../docs/design-standard.md#第三部分core-独立世界引擎运作原理)。

**同步修改规则**：只要 Core 部分源码或结构发生修改，本文件（`csharp/AGENTS.md`）必须同步修改更新，确保规范与实际使用结构完全一致。

## 现行核心架构与模块结构

Core 是完全独立的 .NET 类库，保持纯粹性，与渲染引擎、实时循环、几何与 UI 分立。包含以下现行使用结构：

- **本体与认知投射模型（Model.cs, WorldRuntime.cs）**：
  - `Essence`：客观对象身份，物理状态与机制由挂载的 `Property`（如 `SpatialProperty`, `StateProperty`, `LockProperty`）持有。
  - `Projection`：主观认知投射，记录在 `ProjectionWorld` 中，对象身份使用引用，结构精炼。
  - `SoulEssence`：拥有专属的私有 `ProjectionWorld`，保存主体的认知、规则与历史。
  - `WorldRuntime`：引擎权威现实管理与现实/认知投射映射。支持 `Project`, `Associate`, `Sync`，但不暴露给角色决策直接读取。
- **观察、推理与心智转变（Cognition.cs, Reasoner.cs, Speech.cs）**：
  - 主观认知更新统一经由 `Observation.Observe` 接口驱动，认知集合保持封闭。
  - `Cognition.Infer` 沉淀支持的显式命题；`Cognition.Corrupt` 通过输入管道施加 `IProjectionTransform`。
  - `Reasoner` / `CaseModel` / `CaseBelief`：为共享案件计算表与主观推演过滤层，支持证据链与假设排除；专用内容的现有残留以源码审计标记定位。
- **抽象语法树与解析器（Ast.cs, AstDecoder.cs）**：
  - 核心包含 `Noun`（`Reference`, `Slot`）、`Condition`（常量、存在、标签、关系、位置、逻辑组合）、`Effect`（操作、设置、增减、临时租借、序列、等待、发言、思考）和 `Rule`（Require, Forbid, Permit, Prefer 模式及权重）。
  - AST 严格使用类型化节点、槽位、Tag 和 Projection 引用；字符串仅用于名称与描述。
  - `AstDecoder` 将文本规则解码为强类型 AST。
- **调度、执行与临时状态（CTBSystem.cs, ActionRuntime.cs, SimulationSession.cs, TemporaryState.cs）**：
  - `CTBSystem`：负责离散行动值（CTB）的延迟与激活调度。
  - `ActionRuntime`：两阶段事务机制（先 `Prepare` 后 `Commit`），状态变更仅在提交阶段权威生效。支持普通带时效的 `TemporarySet` 以及执行生命周期绑定的 `TemporarySetUntilEnd` 状态租约覆盖。
  - `SimulationSession`：协调主动/自主动作，支持 `wait_until` 条件挂起恢复、任务取消（`Cancel`）与幂等清理（`Dispose`）。
- **决策与社交评估（DecisionSystem.cs, SocialSystem.cs）**：
  - `SocialSystem.EvaluateSender` 为菜单提供只读预评估；与收到请求后的 `Evaluate` 共用配合、发送者信任、历史与宿主传入的行动相合分，不生成预览认知事件。
  - 请求相合分可通过 `RequestCompatibility` 标签写入接收者的请求投影；`Evaluate` 未显式传分时读取该标签，使自主决策与菜单预评估保持同一公式。
  - `DecisionSystem` 与 `SocialSystem` 严格基于主体自身内部世界的 ProjectionWorld 展开演算与决策，绝不直接依据客观现实做抉择。
  - 规则模式中：`Prefer`/`Require` 提出候选与加减分，`Forbid` 实施封禁，`Permit` 依权重放行禁令。
- **无剧情硬编码铁律**：
  - 严禁将剧情写入引擎代码；剧情仅存在于 `.story` 和译文。核心逻辑不得硬编码剧情内容或特定剧本文案。

## 独立打包边界

- Core 作为独立 .NET 类库构建与打包；包元数据和 API 说明与当前 C# 源码保持一致。
- Core 的包说明维护在本目录 `README.md`。
- Core 的 NuGet 打包输出统一保存在仓库根目录 `artifacts/releases/core/`。
- 构建命令：`dotnet build csharp/Core.sln -c Release`
- 打包命令：`dotnet pack csharp/DualOntologyWorldEngine.Core.csproj -c Release -o artifacts/releases/core`

## 目录纯净性与回归验证

- 本目录专属于 Core 独立逻辑与核心基准测试，游戏呈现与交互资源属于 `godot_csharp/`。
- 核心级基准与验证脚本就地保存在 `csharp/` 根层：
  - `dotnet run --project csharp/ScopeRegression.csproj`
  - `dotnet run --project csharp/ReasonerRegression.csproj`
- 系统演进直接在既有架构与文件上持续重构。
- 源码审计遵循根目录的 `STORY-HARDCODE` 编号、类型与范围约定。仅新增审计注释时，逐文件核对去除标记后与基线一致，并构建 Core；审计清单位于 `artifacts/story-hardcode-marking/`，不把源码中的具体内容写入本规范。
