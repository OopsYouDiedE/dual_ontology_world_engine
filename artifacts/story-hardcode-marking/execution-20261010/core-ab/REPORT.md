# core-ab 执行报告

日期：2026-10-10。范围：AR-CORE-01 A 与 B 的 Core 基础；以 `control/contracts-phase02.md` 为准，吸收消费方 R2 与 CB 验证要求。

## 改动与原因

- 四域分开：客观 `EntityId`、切面 `FacetId`、作者词项 `AuthorTermId`、主体 `SubjectProjectionId`；作者 ID 拒绝空值、首尾空白及非 NFC，按 Ordinal 比较。旧构造分配 opaque ID。删除、遗忘、失败提交的已用保留句柄均不复用，同名新建不重绑。
- Observation 先冻结输入、校验整批及最终闭合图，再在主体 gate 内发布。非空成功批次只增一版，空批次不增，失败不发布认知/绑定。绑定先于回调；通知按版本排队，重入回执 Pending，最终 Completed，订阅者异常进入 NotificationFaults。
- 完整不可变快照包含名称、kind、existence、parent、tags、正/否关系、规则权重/接受、procedure、KnownTerms、可见来源与历史；AST 只留主体句柄，不保存源 Projection、Essence、runtime 或回调。
- AST、Decision、Social、Reasoner 的一次求值捕获同一版本，复用该版本的脱离源对象的读图；兼容结果映回原运行时引用。Social/Reasoner 的既有公式、当前关系/当前信任读取语义保留。
- 引擎追加不可变审计前后态、历史差分与遗忘 tombstone，可重建精确版本。遗忘清除可见结构依赖、历史及 LastObservation，不从审计补回；审计不接入决策。
- 中文词项桥仅查询主体已获知词典，保留歧义和 Unknown，区分六种状态及 Stale 错误；消歧后绑定已有 Projection，实测进入既有 SimulationSession/Inspect。
- 新增两个中性世界回归：平行空间与嵌套空间；`tools/verify.py` 在 scope 后增加 cognition，Core 项目排除回归入口，同步 `csharp/AGENTS.md`。没有新增具体剧情内容。

## 新公开 API 与兼容入口

| 范围 | 新公开接口/类型 |
|---|---|
| 身份 | 四域 ID；`Essence(EntityId, ...)/Id`、`SoulEssence(EntityId, ...)`、`WorldRuntime.Project(Essence, FacetId, ...)`；`Projection.SubjectId/KnownTerms` |
| 精确已知查询 | `WorldRuntime.KnownProjections`、`KnownFacet`、`LookupSingleKnown`；未知切面、未登记切面、歧义分开，隐藏删除不清除已知集合 |
| 提交/通知 | `Observation.ObserveBatch`、`ObservationChangeV1`；`ProjectionWorld.SubjectWorldId/AllocateSubjectProjectionId/Revision/VocabularyRevision/CognitionChanged/ReadNotificationReceipt`；`CognitionCommitReceiptV1/CognitionNotificationReceiptV1/CognitionChangedV1/NotificationDeliveryV1`；`ContractResult<T>/ContractError/ContractErrorCode/CognitionContractException` |
| 快照/AST | `ProjectionWorld.CaptureSnapshot`；`CognitionSnapshotV1/FrozenProjectionV1/ObservedStateV1/VisibleObservationV1/RelationEdgeV1/KnownAuthorTermV1/SourceLocationV1/ObservationTimeUnitV1`；`FrozenNounV1/FrozenConditionV1/FrozenEffectV1/FrozenRuleV1`；`CognitionGraph.Rebind`；`CognitionAst.FreezeCondition/Effect/Rule` 与 `RebindCondition/Effect/Rule`；`Ast.Evaluate(snapshot, ...)/Equivalent(Frozen..., Frozen...)`，`SnapshotActionContextV1/SnapshotTruthV1` |
| 消费 | `DecisionSystem.Assess(snapshot, actor)`、`SocialSystem.Evaluate/EvaluateSender(snapshot, ...)`、`Reasoner.Assess/Weight(snapshot, ...)`；旧评估结果增加可空 Revision，新决策 DTO 同时带词典版本 |
| 审计 | `WorldRuntime.CaptureCognitionAudit/CaptureCognitionGraph/ReadCognitionAudit/ReconstructCognitionAudit`；完整审计 DTO、含已分配/退休句柄的 `CognitionGraphArchiveV1` |
| 词项 | `ChineseTermBridge.ValidateSyntax/Interpret`、`TermQueryV1/TermInterpretationV1/TermCandidateV1`、`WorldRuntime.BindResolvedTerm` |

旧 Essence/Soul 构造、Project、Observation.Observe、运行时引用型 AST/Decision/Social/Reasoner 入口保留。`KnownProjection`、View 与现有执行器的旧首切面入口未迁移，调用方应在后续宿主迁移时选择新精确接口。没有新增效果种类。

`ObservationInput` 新增 `KnownTerms/Origin/TimeUnit/SourceDependencies`；`KnowledgeRevision/VocabularyRevision` 独立；新决策返回 `SnapshotDecisionAssessmentV1`。审计类型为 `CognitionAuditChangeV1/CognitionAuditCommitV1/CognitionAuditHistoryV1`；脱离源对象的图为 `DetachedProjectionGraphV1`。Fork/codec 的预留类型见后续范围。

## 最终验证

最终源码由全新、独立上下文 Agent 验证，未读取设计规范。生产代码最终验证日志：`tmp/core-ab-validation-read-cost/`；最后补强的 CB-V05 独立验证日志：`tmp/core-ab-validation-score-coverage/`。前轮日志保留，不代替最终证据。

| 命令/步骤 | 结果 |
|---|---|
| `dotnet build csharp/ScopeRegression.csproj -c Release` | 统一入口成功，0 警告、0 错误；复验前另外 Rebuild 清除旧共享 apphost |
| `dotnet run --project csharp/ScopeRegression.csproj -c Release --no-build` | `TOTAL 24 checks` |
| `dotnet run --project csharp/CognitionRegression.csproj -c Release` | `TOTAL 202 checks`，两种世界全部通过；最后 CB-V05 补强复验 exit 0，7.70 秒 |
| Scope 在 Cognition 前后 `--no-build` | 两次均 `TOTAL 24 checks`，没有跨程序污染 |
| `dotnet run --project csharp/ReasonerRegression.csproj -c Release` | `TOTAL 38 checks` |
| `python tools/verify.py --only core scope cognition --keep-going` | 最后测试补强后 exit 0，9.18 秒；默认 Scope 24 项，Cognition 202 项 |
| `python tools/verify.py --skip gallery --keep-going --godot <用户指定路径>` | exit 1；core/scope/cognition 通过（24/202）；默认 host exit 1；flow/flow2 崩溃且无断言终表；114.78 秒 |
| `dotnet build godot_csharp/DualOntologyWorldEngine.sln --no-restore -m:1` | 成功，1 个 NU1900 网络警告、0 错误，6.52 秒，生成最终生产源码宿主 |
| Godot `--headless --editor --path godot_csharp --import --log-file <工作区路径>` | 前轮 import exit 0，1185 steps 完成，60.18 秒；最终轮复用导入缓存 |
| Godot flow：同脚本加工作区 `--log-file` | `TOTAL 134 checks, 24 failed`；110 通过，exit 1，383.06 秒，完整终表，无退出崩溃 |
| Godot flow2：同脚本加工作区 `--log-file` | `TOTAL 103 checks, 99 passed, 4 failed`；exit 1，265.04 秒，完整终表，无退出崩溃 |

默认并行宿主构建输出仅 `Build FAILED / 0 Warning(s) / 0 Error(s)`；先前诊断定位在并行项目引用还原，`-m:1` 还原成功，底层异常未输出。NU1900 表示无法访问 NuGet 漏洞数据索引。没有更改构建配置或 verify 的原 host 命令。

Godot 默认 `user://logs` 写入受阻，两项均以 `0xc0000005` 退出且无断言终表；各改用工作区日志重试一次。前轮首次 flow 还因缺少 `.ctex` 缓存而缺终表，完成 import 后复核。完整终表按输出判断，不为既有失败反复运行。根证书读取和 appdata 编辑器设置写入仍受阻。最终日志：`07-flow-attempt-1.*`、`08-flow2-attempt-1.*`；默认 verify 分步字节保存在 `unified-verify-step-logs/`。

首轮 flow/flow2 比给定 24/3 基线分别多 1/5 项失败。只替换 Core DLL/PDB 的 HEAD 对照：flow 为 134/24（321.61 秒），flow2 为 103/4（110.30 秒）；完整终表，无退出崩溃。对照后当前 DLL/PDB 已在 finally 恢复并逐字节核对。日志：`tmp/core-ab-baseline/logs/`。

逐条含重复项比较：首轮 flow 额外失败为 R-06 挂钩线索；flow2 额外失败为 Main 60 秒静止超时、action 有限完成、重演步子/吻合、回手稿。超时回调使该轮多出第 104 项检查。没有改断言。减少内部评分的重复 AST 冻结/重绑定，并复用提交完整校验生成的同版本读图后，两项最终失败名称及次数与旧 Core 对照完全相同，新增/移除均空；新增失败全部消除。

给定 flow2 基线为 3 项，本沙箱旧 Core 对照为 4 项，不能报成 3 项通过对齐。当前四项是：本轮章一终局文件写入、正式 .story 开场/首课/自由行动台词、首个可操作状态在讲堂、到案发次日傍晚。最终重演原始输出为 `ok 重演有墨影步子（27），全吻合=true`，回手稿及有限完成检查通过。完整比较见 `comparison-summary.json`。

两项最终 stderr 与各自旧 Core 对照逐字节一致：flow 仅根证书错误；flow2 另有“交互地点已不在当前空间”和 escort 无效的既有报错。没有新增引擎错误，未改 UI。

## 用例覆盖与原始输出摘录

下表本批范围在 flat/nested 两种结构全部通过。摘录 flat 的原始 PASS 行；nested 对应行亦 PASS。CB-V07 的 wire codec 与跨进程部分、CB-V09 的 C 部分、CB-V11 的跨进程部分明确延期。

| 用例 | 本批覆盖 | 代表性原始输出 |
|---|---|---|
| CB-V01 | 100 次完整图/AST 读取与 30 次原子写入，所有字段同态 | `PASS flat CB-V01 atomic full graph + AST (100 reads/30 commits)` |
| CB-V02 | 最后输入跨域、父环、重复目标、默认数组、失败引擎绑定均回滚 | `PASS flat CB-V02 foreign last input rollback` |
| CB-V03 | 完整绑定、新切面回调可见、重入顺序、回执与异常 | `PASS flat CB-V03 complete binding, reentry order, Pending/Completed + NotificationFaults` |
| CB-V04 | 旧图各字段/AST 不变；遗忘清除可见依赖，审计保留并重建 | `PASS flat CB-V04 immutable audit + tombstone + old reconstruction` |
| CB-V05 | 规则与请求用不同目标；80 轮/160 个评分，四种模式、槽位上下文、社交及历史固定一版 | `PASS flat CB-V05 full Assess fixed revision (80 rounds/30 writes, policy + distinct social request)` |
| CB-V06 | 社交预览/实际同图同历史，预览只读，遗忘不读审计；Reasoner 当前关系/来源/信任 | `PASS flat CB-V06 social same snapshot + history, preview read only` |
| CB-V07 | 全 AST、嵌套 Request/Wait/Think、重复引用/槽位两阶段重绑；未知节点/default/enum/跨域及间接缺 parent 拒绝 | `PASS flat CB-V07 indirect missing parent rejected as InvalidReference` |
| CB-V09 | 同一已知模型/快照，隐藏现实变形不改变词项、评分、Reasoner；DTO 无客观反查字段 | `PASS flat CB-V09 hidden objective deformation cannot change interpretation/scoring` |
| CB-V11 | 多实体/切面/主体、同名替换、精确已知集合、删除不暴露、忘后重认、ID 不复用与增长测量 | `PASS flat CB-V11 distinct known facets, no first-item fallback` |

另验证词项 Ambiguous/Unknown/Unrecognized/Unsupported/InvalidReference/Resolved、限定消歧进入执行器、版本过期、默认 ID、失败保留句柄退休、空批次、越界审计/通知版本及损坏审计拒绝。未修改既有测试预期。

最终测量摘录（Windows、实际 SDK 10.0.401、net8.0 Release；内存为托管保留量差值，非峰值；样本测量，非性能断言）：

```text
MEASURE flat seed=0 entities=16 facets=32 subjects=3 operations=144 elapsed_ms=66 memory_delta_bytes=336536
MEASURE nested seed=0 entities=16 facets=32 subjects=3 operations=144 elapsed_ms=60 memory_delta_bytes=336048
TOTAL 202 checks
```

## 修复轮次与证据

- 独立第一轮发现 transform 的外部 Parent 被过滤而未拒绝；现先校验所有引用再做既有感知抑制，失败不发布绑定。日志：`tmp/core-ab-validation/`。
- 第二轮执行器断言失败来自中性 fixture 缺少 SpatialProperty；补齐客观空间属性，不更改断言或执行器。日志：`tmp/core-ab-validation-round2/`。
- 新增末端测试曾因局部变量重名 CS0136 编译失败，仅重命名；保留失败日志 `tmp/core-ab-validation-verify/`。修正后独立轮 200 checks 通过：`tmp/core-ab-validation-latest/`。
- 审阅补齐多层 parent 缺引用的合同错误及回归，最终轮单独验证，旧轮结果不代替最终证据。
- 恢复后核对发现旧统一回归 scope 误启动 Cognition，原因是同目录项目共用 obj/apphost；该项旧证据作废。新 Cognition 项目在 SDK 导入前隔离中间/输出目录，复验必须在运行 Cognition 后仍得到 Scope 的 24 项输出。
- 此前完整宿主默认构建失败；单节点 restore/build 成功。诊断及原始字节：`tmp/core-ab-validation-final/`。所有原始日志为临时验证材料，交付报告只摘录必要结果。
- Godot 导入曾由主代理中断（exit 4294967295）；其产生的三份 `generated_lines_zh.*.translation` 已按导入前干净工作区证据恢复，交付不包含这些生成物。
- Godot 对照暴露首轮新增失败后，减少同版本读图的重复构建与内部 AST 转换；没有放宽输入校验、丢弃历史或改变评分公式，重新执行机制与宿主回归。
- 最后补强 CB-V05：规则与请求效果分用两个目标，避免去重抹掉请求社交分；独立复验覆盖两种候选及其原运行时引用，202 项全部通过。仅改测试，生产代码未变。

## 后续接口、未完成项与风险

- 保存/恢复 codec 未实现：后续可基于 Frozen AST、快照、完整审计、GraphArchive 的 AllocatedIds 与两阶段 Rebind 增加协议、限额、版本兼容及跨进程恢复；`CodecLimitsV1` 仅保留合同类型，无序列化能力。
- C 未实现：现有 Rebind 是同域脱离源对象的图重建，不是隔离模拟；`CognitionForkV1` 仅接口类型，不承诺 fork/模拟效果隔离。CB-V09 只覆盖固定主体图的隐藏变形。
- D 未实现：保留旧 Event kind、来源与社交计算；认知提交通知不是游戏事件迁移。CB-V08、CB-V10、CB-V12 未执行。
- 宿主正式 IR、持久身份供给、存档以及精确切面调用迁移留给下一批；兼容旧入口仍使用 opaque ID，不能据此宣称跨进程稳定。
- 旧可变字段的自由组合读取、Reality.Sync 及整个 WorldRuntime 不承诺全局跨线程一致；保证边界是主体批次与单次快照求值。遗忘依赖由结构引用/显式 SourceDependencies 决定，不分析任意文本含义。
- 审计 API 属于引擎侧合同边界，不是访问控制系统；新决策/词项 API 不读取它。增长测量只是样本，无性能门槛承诺。
- 完整 Godot 回归用时高于同机旧 Core 对照（flow 383.06/321.61 秒，flow2 265.04/110.30 秒）；虽未新增失败，整图校验/快照与审计的运行成本仍需后续实测和优化。
- 验收级别：本批仅自动机制回归；一级三份全程记录、二级 UI 专项、三级独立盲玩均未执行。人物合理性、剧情及体验未验证。
- 未读取设计文件，未修改 UI/布局资源，未运行剧本编译器，未提交/推送；按本轮指令留给主代理处理。
- 已审查本轮生产源码及 diff，无遗留 DEBUG 或临时打印；中性回归的 PASS/EXPECTED/MEASURE/TOTAL 是正式验证输出。
