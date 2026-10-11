# Core 维护规范

本目录为独立 Core 类库，使用 .NET 8、C# 12、nullable reference types 和 implicit usings。保持纯粹的逻辑演算，不依赖 Godot、渲染引擎、几何与 UI。世界引擎原理详见 [统一设计规范](../docs/design-standard.md#第三部分core-独立世界引擎运作原理)。

**同步修改规则**：修改 Core 架构、模块合同、命令或排错方式时，必须同步更新本文件，保持与实际架构一致。

## 测评方式约束

- 严格遵循根目录用户裁定：禁止流程回归测评，禁止以“xx 项通过”、断言数量、通过率或“全绿”评价机制或代替验收。
- 机制与基准检查仅用于定位具体工程问题，必须解释实际输入、执行与结果，不得以此证明人物行为、章节推进或修改效果。
- 共同决策的改善必须在正式连续会话中核对主体实际可知信息、所选行动、真实反馈与玩家可见结果。

## 核心架构与模块职责

1. **本体与认知投射（Model.cs, WorldRuntime.cs, StableIdentity.cs）**：
   - `Essence`：客观对象身份，物理状态与机制由挂载的 `Property`（如 `SpatialProperty`, `StateProperty`）持有。
   - `Projection`：主观认知投射，以 `SubjectProjectionId` 为稳定句柄，不公开反查现实。
   - `SoulEssence`：持有私有 `ProjectionWorld`，隔离各主体的认知、规则与记忆。
   - `WorldRuntime`：权威现实管理及现实与认知投射的映射同步，不直接暴露给角色决策。
   - `StableIdentity`：实体 ID、切面 ID、作者词项 ID 与主体句柄严格分域，显式持久 ID 规范化并使用 Ordinal 比较。

2. **认知、观察与快照（ProjectionWorld.cs, Observation.cs, CognitionSnapshot.cs, SnapshotGraph.cs）**：
   - `Observation`：主观认知更新的统一入口，认知写入原子提交与闭合校验。
   - `CognitionSnapshot` / `SnapshotGraph`：主体快照为不可变闭合图，仅包含主体句柄，不含客观身份或源对象引用。
   - `CognitionAudit`：引擎专用追加审计历史，支持按版本精确追踪差分与状态重建。
   - `ChineseTermBridge`：在主体快照已知词项中解释词形与作者词项，消歧后进入执行器。
   - `TypedEvents` / `CognitionEventCodec`：行动、观察、证词、传播与呈现事件强类型化，权威记录与主体投影分离持久化。

3. **推理、信念与心智（Cognition.cs, Reasoner.cs, Speech.cs, SubjectiveReasoning.cs）**：
   - `Cognition.Infer`：沉淀主体显式命题；`Cognition.Corrupt` 提供主观认知变换管道。
   - `Reasoner` / `CaseModel` / `CaseBelief`：证据链计算与假设排除，输出结构化权重与理由代码，不生成文案。
   - `SubjectiveReasoning`：在单主体不可变图中进行有界推演、假设组合与信息价值评估；严格使用主体已接受的信念，依据调用者角色预算展开，不读取权威世界或外部答案。

4. **抽象语法树（Ast.cs, AstDecoder.cs, FrozenAst.cs）**：
   - 包含强类型 `Noun`、`Condition`、`Effect` 与 `Rule`（Require, Forbid, Permit, Prefer, Infer 模式）。
   - 严格使用类型化节点与投影引用，字符串仅用于名称与描述。
   - `Rule` 的 `LifetimeScope` 与 `LifetimeOwner` 独立于实体作用域，由宿主精确管理撤销。
   - `AstDecoder` 负责文本规则解码为强类型 AST。

5. **调度、执行与临时状态（CTBSystem.cs, ActionRuntime.cs, SimulationSession.cs）**：
   - `CTBSystem`：离散行动值（CTB）的延迟与激活调度，支持批量重调度。
   - `ActionRuntime`：两阶段事务机制（`Prepare` 与 `Commit`），状态变更仅在提交阶段生效；支持普通与执行生命周期绑定的状态租约。
   - `SimulationSession`：协调主动/自主动作，管理条件挂起与幂等清理。

6. **主体决策与行为学习（SubjectiveAgent.cs, SubjectiveAgentState.cs, SubjectiveDecision.cs, SocialSystem.cs, ActionLearning）**：
   - `SubjectiveAgent`：玩家席位、NPC 与其他主体共用的语义行动政策；输入为本人快照、推理预算、动机与可见方法机会。
   - `SubjectiveAgentState` / `SubjectiveAgentStorage`：持久化主体的经历、已知方法、证明基线与回执；状态按变更片段原子持久化，支持版本化编解码。
   - `ActionLearning`：负责 AST 与语义 offer 的双向转换，转接共同决策政策；真实阶段由宿主提交稳定回执。
   - `SocialSystem`：基于主体私有认知展开社交请求评估与相合度计算，不直接依据客观现实。
   - `SubjectRead` / `SnapshotConsumers`：单次求值复用同一不可变快照读图，保证评估一致性。

7. **严禁硬编码剧情**：
   - 剧情与台词仅存在于 `.story` 和译文中。Core 核心逻辑严禁包含任何剧情文案、特定角色对白或预设答案。

## 常用命令与构建

- **编译类库**：`dotnet build csharp/Core.sln -c Release`
- **打包 NuGet**：`dotnet pack csharp/DualOntologyWorldEngine.Core.csproj -c Release -o artifacts/releases/core`

## 工程排错与机制检查

以下独立项目仅用于定位 Core 具体模块的底层逻辑或序列化问题，**不得作为游戏推进、人物行为或验收通过的依据**：

- `dotnet run --project csharp/ScopeRegression.csproj`：作用域机制排错。
- `dotnet run --project csharp/ReasonerRegression.csproj`：推演器基础逻辑排错。
- `dotnet run --project csharp/CognitionRegression.csproj -c Release`：身份、原子认知、快照与编解码排错。
- `dotnet run --project csharp/SubjectiveReasoningRegression.csproj -c Release`：主体推理、多步深度与假设隔离排错。
- `dotnet run --project csharp/SubjectiveDecisionRegression.csproj -c Release`：主体决策反馈、经历 wire 与存档恢复排错。
- `dotnet run --project csharp/SubjectiveAgentRegression.csproj -c Release`：主体 Agent 语义机会、回执幂等与状态 wire 排错。

## 可选同步性能诊断

- `CorePerformance.Begin(stage)` 提供线程局部的可选诊断作用域；无显式订阅时不产生开销，不改变主体状态。
- 覆盖主体读图构建、快照校验、推演初始化、决策评分与状态持久化等关键阶段。
- 仅供排查性能热点，不进入存档或公开状态；阶段耗时不能简单相加为总 CPU，不作为整体帧率改善证明。
