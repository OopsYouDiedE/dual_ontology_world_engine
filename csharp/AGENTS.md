# Core 维护规范

本目录使用 .NET 8、C# 12、nullable reference types 和 implicit usings。世界引擎原理详见 [统一设计规范](../docs/design-standard.md#第三部分core-独立世界引擎运作原理)。

**同步修改规则**：只要 Core 部分源码或结构发生修改，本文件（`csharp/AGENTS.md`）必须同步修改更新，确保规范与实际使用结构完全一致。

## 现行核心架构与模块结构

Core 是完全独立的 .NET 类库，保持纯粹性，与渲染引擎、实时循环、几何与 UI 分立。包含以下现行使用结构：

- **本体与认知投射模型（Model.cs, WorldRuntime.cs）**：
  - `Essence`：客观对象身份，物理状态与机制由挂载的 `Property`（如 `SpatialProperty`, `StateProperty`, `LockProperty`）持有。
  - `Projection`：主观认知投射，运行时继续直接传对象引用；稳定句柄为 `SubjectProjectionId`，不公开反查现实。
  - `SoulEssence`：拥有专属的私有 `ProjectionWorld`，保存主体的认知、规则与历史。
  - `WorldRuntime`：引擎权威现实管理与现实/认知投射映射。支持 `Project`, `Associate`, `Sync`，但不暴露给角色决策直接读取。
  - `StableIdentity.cs`：`EntityId`、`FacetId`、`AuthorTermId` 与主体句柄分域；显式持久 ID 拒绝非 NFC/首尾空白，按 Ordinal 比较。旧入口分配 opaque ID；删除、遗忘与提交失败的已用保留句柄都不复用 ID。
  - `ProjectionWorld.cs` / `Observation.cs`：认知写入经同一 gate 完整准备、校验与原子发布；成功非空批次只增一版。内部前态已闭合，提交校验本批输入及受影响的 parent 链，遗忘扫描并清除依赖；只准备触及节点，未变 Frozen 节点/AST 与可见历史记录共享。引擎绑定先于通知发布，通知按版本排队，重入回执可为 Pending，订阅者异常保存在 NotificationFaults。
  - `CognitionSnapshot.cs` / `SnapshotGraph.cs` / `FrozenAst.cs`：主体快照为完整不可变图，AST/关系只含主体句柄；闭合校验与两阶段重绑定拒绝缺引用（含间接 parent 链）、跨域和未知节点。不含源对象、客观身份或回调；本批没有保存 codec。
  - `CognitionAudit.cs`：引擎专用追加历史使用持久化 ImmutableList，捕获时转合同数组；审计仅比较触及节点，仍保留变化的完整前后态、可见历史差分与遗忘 tombstone，可按精确版本重建；主体可见历史随遗忘删除结构依赖，不从审计补回。
  - `ChineseTermBridge.cs`：仅在指定主体快照的 KnownTerms 中解释词形/作者词项及限定条件；保留歧义和 Unknown，版本失效明确拒绝。消歧目标仍进入既有执行器。
  - `TypedEvents.cs`：行动、观察、证词、传播与呈现使用独立类型，共用不可变事件字段（事件 ID、时间单位、阶段结果、引用、命题、来源及因果）。权威记录由 WorldRuntime 持有；主体投影须属于感知者且只能保留已知引用、命题与可见因果，快照只保存主体句柄。旧 Event 投影与 ObservationEvent 入口兼容。
- **观察、推理与心智转变（Cognition.cs, Reasoner.cs, Speech.cs）**：
  - 主观认知更新统一经由 `Observation.Observe` 接口驱动，认知集合保持封闭。
  - `Cognition.Infer` 沉淀支持的显式命题；`Cognition.Corrupt` 通过输入管道施加 `IProjectionTransform`。
  - `Reasoner` / `CaseModel` / `CaseBelief`：为共享案件计算表与主观推演过滤层，支持证据链与假设排除；专用内容的现有残留以源码审计标记定位。
- **抽象语法树与解析器（Ast.cs, AstDecoder.cs）**：
  - 核心包含 `Noun`（`Reference`, `Slot`）、`Condition`（常量、存在、标签、关系、位置、逻辑组合）、`Effect`（操作、设置、增减、临时租借、序列、等待、发言、思考）和 `Rule`（Require, Forbid, Permit, Prefer 模式及权重）。
  - AST 严格使用类型化节点、槽位、Tag 和 Projection 引用；字符串仅用于名称与描述。
  - `Rule` 的 `LifetimeScope` 与 `LifetimeOwner` 是独立于实体作用域的生命周期元数据；冻结、重绑定与认知修订保留它们，局部有效期必须有 owner。Core 不调度章节退出，由宿主经认知输入精确撤销到期规则。
  - `ActionResolution.FailedEffect` 保留导致当前阶段失败的实际效果引用；宿主可以只对实际尝试失败的对象补充认知，不能从整份意图猜测已经执行的效果。
  - `AstDecoder` 将文本规则解码为强类型 AST。
- **调度、执行与临时状态（CTBSystem.cs, ActionRuntime.cs, SimulationSession.cs, TemporaryState.cs）**：
  - `CTBSystem`：负责离散行动值（CTB）的延迟与激活调度。
  - `ActionRuntime`：两阶段事务机制（先 `Prepare` 后 `Commit`），状态变更仅在提交阶段权威生效。支持普通带时效的 `TemporarySet` 以及执行生命周期绑定的 `TemporarySetUntilEnd` 状态租约覆盖。
  - `SimulationSession`：协调主动/自主动作，支持 `wait_until` 条件挂起恢复、任务取消（`Cancel`）与幂等清理（`Dispose`）。
  - ActionExecution 的 Events 为引擎回执；提交、成功阶段、操作与终态独立登记。失败阶段不提交效果，取消仅登记终态并停止后续效果；实际操作记录目的位置。Say 的 Claim/Topic 随 AST 冻结、绑定与翻译，发言产生证词，接收请求与执行分立。
- **决策与社交评估（DecisionSystem.cs, SocialSystem.cs）**：
  - `SocialSystem.EvaluateSender` 为菜单提供只读预评估；与收到请求后的 `Evaluate` 共用配合、发送者信任、历史与宿主传入的行动相合分，不生成预览认知事件。
  - 请求相合分可通过 `RequestCompatibility` 标签写入接收者的请求投影；`Evaluate` 未显式传分时读取该标签，使自主决策与菜单预评估保持同一公式。
  - `DecisionSystem` 与 `SocialSystem` 严格基于主体自身内部世界的 ProjectionWorld 展开演算与决策，绝不直接依据客观现实做抉择。
  - `SubjectRead.cs` / `SnapshotConsumers.cs`：一次 AST、Decision、Social、Reasoner 求值固定一份主体快照；内部已验证快照在首次读取时延迟构建同版本读图与引用映射，后续复用，每版使用独立对象壳，不重复校验旧历史/AST。外部快照入口与公开 Rebind 仍完整校验；Frozen AST 依赖遍历只读已验证节点，不为找句柄解冻。兼容入口将结果映射回原运行时引用，并附认知版本。新快照入口不读取 runtime/audit。旧字段组合读取与 Reality.Sync 不承诺整图并发一致，Core 不承诺整个 WorldRuntime 跨线程安全。
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
  - `dotnet run --project csharp/CognitionRegression.csproj -c Release`：两种中性结构验证身份、原子认知、快照、审计、词项与整轮评分；评分用不同目标同时保留规则与社交请求候选，覆盖槽位上下文及历史分，避免候选去重掩盖混读。增量提交回归覆盖读图缓存后的连续追加、父环失败不发布、嵌套 Wait/Say 依赖遗忘、空状态差分与旧版审计重建。EventRegression 覆盖五种事件的权限投影、快照依赖、稳定 ID、发言与接受不提交效果、失败/取消、实际到场及呈现改名。codec/跨进程恢复、主观推演隔离由后续批验证。
- `tools/verify.py` 在 scope 后运行 cognition 步骤；Core csproj 排除各独立回归程序源，避免把测试入口打入类库。
- CognitionRegression 在 SDK 导入前设置独立 `obj/CognitionRegression/`、`bin/CognitionRegression/`，避免共享 apphost/还原缓存导致启动其他回归入口。Scope/Reasoner 原构建路径保留。
- 系统演进直接在既有架构与文件上持续重构。
- 源码审计遵循根目录的 `STORY-HARDCODE` 编号、类型与范围约定。仅新增审计注释时，逐文件核对去除标记后与基线一致，并构建 Core；审计清单位于 `artifacts/story-hardcode-marking/`，不把源码中的具体内容写入本规范。
