# 已发布快照读图缓存：实施前独立差异审查

审查日期：2026-10-11。复核结论：**可实施这个限定范围的缓存方案；代码尚未应用，针对性执行验证尚未完成。** 原始提案的“需修改”结论及其原因保留在下面，最新待审文本已经处理该问题。

审查对象为尚未应用的 `cache-proposal.patch`。初审 SHA-256 为 `2C05A0CE3E30AF73F53DEE2AE7666D563F1CE32DC4D38066EA6EAB4E6B2A2B0C`；复核修订文本 SHA-256 为 `D928E106AE631D1EA250B013C14E885D721F03A85361623C1387066B467775B0`。本审查只读取提案、相关 Core 源码和 Core 工作约束；没有读取设计文件、剧本、谜底、历史试玩或诊断流。没有修改源码、运行游戏、构建或执行回归。仓库已有工作区修改，审查依据为本轮读取到的工作区源码，不把它们当作该提案新增的内容。

## 原始提案必须修改的问题与修订复核

`SubjectiveReasoner.BuildExternalGraph` 原先把空快照交给 `SnapshotGraph.Build`，由其开头的 `Contract.Require(snapshot is not null, MalformedData, "snapshot", ...)` 抛出 `CognitionContractException`。提案先调用 `PublishedRead(snapshot)`；`ConditionalWeakTable.TryGetValue` 对空 key 会抛出 `ArgumentNullException`，因此改变公开构造函数原有的无效输入合同错误类型与诊断路径。

建议让 `PublishedRead` 对 null 返回 null，再由原有 `SnapshotGraph.Build` 执行原合同检查；可以把其参数标为 nullable，并在 `TryGetValue` 前做 null 判定。保留 `BuildExternalGraph` 先验证预算的既有顺序。不要用捕获所有异常或放宽 `Build` 验证的方式处理。

这是合同兼容性修正，不影响缓存命中时的图复用，也无需改变预算、知识输入或正确条件。

复核确认：最新待审文本改成 `snapshot is not null && PublishedReaders.TryGetValue(snapshot, out var graph) ? graph : null`。空输入不会进入弱表查找，返回 null 后由 `?? SnapshotGraph.Build(snapshot)` 触发原 `MalformedData` / `snapshot` 合同异常。ValidateBudget 仍先执行。这处静态差异满足初审修改要求；没有执行该代码验证异常结果。

## 同一快照与失效边界

全仓库 C# 调用检索中，`SnapshotGraph.BuildRead` 只有 `SubjectRead.Capture` 一个调用位置（`csharp/SubjectRead.cs:29`）。Capture 在 `world.Gate` 内取得 `world.CaptureSnapshot()` 的实际发布对象，并按世界缓存 ReaderGraph；不会把调用者任意提交的快照直接送入 BuildRead。

初始化快照由 `ProjectionWorld` 创建。正常非空提交在 `Observation.PrepareAndPublish` 中检查输入和父图闭合，构造新的 `CognitionSnapshotV1`，然后在同一发布阶段更新 `world.Snapshot` 并清空 ReaderGraph（`csharp/Observation.cs:263`、`:264`）；原读图中的壳对象与活世界对象不同。空提交不更换快照，符合复用前提。运行时恢复使用 `SnapshotGraph.Build(..., detached:false)` 验证外部快照后建立世界；其他 Build 调用不会登记进本提案的缓存。

ConditionalWeakTable 按对象身份匹配，不按 record 的值相等、主体 ID 或修订号匹配。因此旧快照即使仍被持有，也只取到自己的旧图；`with` 副本、解码副本、另一主体对象及另一修订对象不能命中旧条目。旧快照仍可读是保留历史快照语义，不是失效错误。缓存的值引用 key 不会把这类弱表变成普通强引用字典。

提案 `GetValue(snapshot, _ => graph)` 后仍返回本地 graph。如果同一个快照被两个已验证世界分别 Capture，缓存会保留其中一个完整图，各世界可能保留各自的等价读图。这没有观察到语义错误，但不能宣称所有世界必定共用唯一图实例。不要为了强制唯一实例把可写活世界图登记进去。

## 实际写入与公开引用

`DetachedProjectionGraphV1` 和 Projection 壳并非类型层面的不可变对象；Projection 字段仍有 internal setter，图的 World 也有内部可写状态。因此本结论依赖当前调用链确实只读，不是因 Detached 标志提供了写保护。

`SubjectiveReasoner` 只读 `_graph.Projections`、投影字段及引用关系；Normalize/References/Unbound 经 Thaw/Map/Freeze 创建 AST 节点，Learn 只写该推理器的 `_beliefs`/`_claims`，Prove/Explore 只写该实例的探索计数、停止原因与 `_completedProofs`。没有把假设、推导或反馈写回图。`Ast.EvaluateLive`（`csharp/Ast.cs:73`）只读取条件与上下文；其递归分支没有 Effect 执行或认知提交。

其余 SubjectRead 消费者 DecisionSystem、SocialSystem、Reasoner 与 SubjectiveDecision 的读图路径也没有提交到读图。DecisionSystem 返回动作时通过 `view.Original` 把效果和依据还原为活世界引用；真实执行与 SaveState 针对原世界。SubjectiveReasoner 的公开结果只有冻结命题、主体句柄与结果字段，不返回 `_graph` 或 Projection。

现有旧 Reasoner.Assess 的 ReasonBasis/EvidenceWeight 在证词情况下可能携带 detached Speaker 引用；这不是提案新增的返回渠道。其公开字段不可写、Projection.World 不公开，当前没有发现仅凭这些引用写入缓存图的公开 API 路径。仍须避免将缓存图 World/Projections 作为新的公开返回值或送入内部 Observation.ApplyBatch/ActionLearning.SaveState。公开 CognitionGraph.Rebind 继续返回独立 Build 图，与缓存无关。

## 外部合同、并发、预算与信息隔离

除上面的 null 错误类型外，未发现外部输入校验绕过：缓存命中只能来源于内部 Capture 发布过的同一个快照对象；未登记对象仍调用完整 `SnapshotGraph.Build`。公开 Rebind、AST 快照求值、词项解释、快照决策和社交入口仍执行 Build。公开 SubjectiveReasoner 的 actor、target/tool/scene 绑定以及后续条件的 Thaw 引用验证仍执行。调用者对发布快照创建恶意 `with` 副本不能继承缓存身份。

读图全部字段填充完毕之后才加入弱表，ReaderGraph 创建受世界 Gate 保护；ConditionalWeakTable 自身支持并发查找/登记。一个线程在登记前查询不到条目只会走完整 Build，不会拿到半成品。不同推理器可并行只读同一图，探索状态互不共享。**同一个 SubjectiveReasoner 实例的并发 Prove/Explore 原本就没有线程安全保证；该提案没有解决或新增这种使用许可。**

ValidateBudget 和每次 Spend 没有改变，证明缓存仍属于各推理器实例，弱表不缓存证明或决策。没有提高 Depth/Expansions/Hypotheses、跳过探索消耗、跨角色复用证明或搬入现实对象、审计历史/答案。图仍只由传入主体快照内容构成；缓存不引入另一主体或客观世界的数据。性能收益是否足以改善正式游玩尚未验证。

## 实施后的针对性验证

以下为独立工程边界验证建议，不执行流程回归、不按断言数量评价，也不能替代正式三级会话：

1. 以有效预算分别传入 null 快照、未登记的畸形快照，以及已发布快照的畸形 `with` 副本。记录具体异常类型、ContractError.Code/Path；它们应与原 Build 路径一致。同时验证无效预算仍先按原顺序报错。
2. 从中性新主体提交一个实际标签/父级状态，Capture 建立读图并创建公开推理器；随后真实提交变更，再创建新推理器。记录旧快照始终读取旧值、新快照读取新值、修订来源；核对节点的引用身份隔离。另行覆盖新建、遗忘和词项修改的发布失效。
3. 为同一已发布快照创建两个不同预算/上下文的推理器，分别执行中性命题与假设探索。记录实际命题结果、展开消耗和停止原因，并对照未命中缓存的同内容独立快照；不能出现相互消费预算、相互继承证明或假设写回。
4. 使用中性数据并行 Capture/创建多个推理器，并在原世界提交新修订；记录每个结果所属的快照修订和值。完整结果应分别属于某一固定快照，不能混合新旧父级、标签或关系。并行测试用独立推理器实例。
5. 保存缓存读图字段的前后内容摘要，执行 Observed/Prove/Explore 与其他 SubjectRead 只读消费者，核对图字段和图 World 的快照引用没有变化；单独记录真实原世界提交改变了哪些字段。避免通过注入状态或预置游戏答案完成验证。
6. 对另一个主体使用域外 actor/context/evidence/condition 引用，记录具体 InvalidReference 拒绝结果；同修订值的另一主体快照与值相等副本不得命中已登记对象的图。保留公开 Rebind 返回图独立于共享缓存的核对。

修订提案后应保留原提案和本报告，单独记录空输入处理差异，再建立明确区分的新保护基线。正式四倍速三级测试须使用守护入口、真实输入/碰撞/交互与原始证据；本报告只作保护文件实施前差异审查，不给出构建、人物行为、UI 或游玩改善的验收结论。
