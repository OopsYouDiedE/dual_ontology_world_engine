# C01+B 基础公共合同 v1（冻结候选 r2）

状态：`PROPOSED_FOR_PROVIDER_CONSUMER_REVIEW`，不是生产租约、实现完成或测试通过。语义授权为 AR-CORE-01；依据本轮 approval-record.md 与 control/contracts-phase02.md。基线 HEAD `232bb99aab6d0caac9dcd14edbead0f27347aefc` 为 root 提供，作者未运行 Git 核对。当前仅前两章约 3–5 游戏日，40 日不在此范围。

## 1. 首批界限与强制后续交付

本首批产物是稳定身份、原子认知提交、完整主体快照、追加审计、closed AST 图 codec 和无源引用重绑定的基础合同。它不提供作者别名词典全桥、词项解释器、多切面 Inspect 执行迁移、Decision/Reasoner 整轮迁移、世界/调度器/宿主全保存或主观推演器。后述消费 API 和保存分阶段矩阵是本批后续必须交付项，不能以本首批图 codec 宣称 B 或 AR-CORE-01 完成。

第一 writer 的精确候选集合为 `csharp/Model.cs`、`csharp/WorldRuntime.cs`、`csharp/Ast.cs`、新 `csharp/StableIdentity.cs`、新 `csharp/CognitionSnapshot.cs`、新 `csharp/CognitionCodec.cs`、`csharp/AGENTS.md`。七文件之外均需另签租约。Core 现有 SDK 自动纳入新 .cs，不必改 csproj。README 的 API 文档同步列为后续文档租约，不能未经授权改写。此稿不修改 AGENTS 或生产。

## 2. 身份与生命周期

四域不相互隐式转换，不共用 ReferentId：

| 类型 | 值与比较 | 所有者/使用边界 |
|---|---|---|
| `EntityId` | 非空 NFC string，Ordinal | objective Essence 持久身份；runtime 的登记表保留活跃和删除项；主体 DTO 不含此值 |
| `FacetId` | 非空 NFC string，Ordinal | 某 runtime 中唯一现实表现；映射到唯一 EntityId，多个 Facet 可指同一 Entity；主体 DTO 不含此值 |
| `AuthorTermId` | 非空 NFC string，Ordinal | 作者词项声明身份；源位置不是 ID；此首批仅类型，不建立主体词典 |
| `SubjectProjectionId` | `{subject_world: Guid, local: Guid}`，两个 Guid 均非空、按值比较 | 一个主体私有图的稳定句柄；没有公开回查现实实体或切面的路径 |

`Guid` 统一 wire 格式为小写 D 格式。NFC 是输入约束：新持久身份边界拒绝非 NFC、空/全空白、前后空白，返回 InvalidIdentity；不静默 Trim、Normalize、大小写/全半角/简繁转换。允许字符串内部空格。默认 struct 值无效，所有边界拒绝它。采用 NFC 而拒绝非规范输入避免规范化碰撞后偷偷换绑；编译器后续可显式报告原文→规范候选，但不得静默覆盖。

为后续 C03 词项解释保留 `KnownAuthorTermV1(TermId,RawLexeme,CanonicalLexeme)`：Projection.KnownTerms、ObservationInput.KnownTerms（null=未更新，非null=完整替换）、ObservationChangeV1.KnownTerms 和 FrozenProjectionV1/ObservedStateV1.KnownTerms。同一节点可获知多词项，同词可指多节点；CanonicalLexeme 必须等于 RawLexeme 的显式 NFC 结果，保留原文，规范化碰撞不合并节点。词项关联只由接收主体的 Observation 输入建立，声明 Entity/Facet/作者 aliases 不自动建立认知，WorldRuntime 不读静态 alias 表补词项。此卡不实现解释/词典索引或 compiler 桥。`VocabularyRevision` 非负 Int64，从0开始；一成功批次若实际改变图成员、节点 Name 或 KnownTerms，恰加1，否则不变。Kind/标签/规则仍可影响限定词结果，因此后续解释凭证必须同时包含 KnowledgeRevision，不能只看 VocabularyRevision。

旧 `Essence(string,kind)`、`SoulEssence(string)` 与 `Project(essence,name,parent)` 保留，分配 `opaque:<Guid>` ID；ID 非显示名称派生，生成后保留、不得复用，随 codec 保存才跨进程稳定。新构造器使用显式 EntityId；新 Project 使用显式 FacetId。同 runtime 的 EntityId/FacetId 唯一性包括 tombstone；另建同名对象须新 EntityId。对同 Essence 新建现实表现须新 FacetId。改名、kind 更新不改上述 ID。

Soul.World 有独立 `SubjectWorldId: Guid`，不是魂的 EntityId。主体 Projection 的 `SubjectId` 非空，现实 Projection 的 `SubjectId` 为 null；新句柄入口拒绝现实投影。`AllocateSubjectProjectionId()` 可生成一次性保留句柄：不改 KnowledgeRevision，不建立认知，失败批次消费的句柄不复用。新批输入 Create=true 使用同域保留句柄。旧输入 Target=null 由内部一次性分配；旧 WorldRuntime.Observe 预分配多投影后按同一事务提交。

Forget 后句柄不再可用于新主观解释、绑定或提交；旧 Projection 引用/引擎客观绑定可保留以承接已提交动作。重新观察建立新 SubjectProjectionId；同 Facet 的 engine `_mental` 当前索引切至新句柄，旧提交绑定保留。不会按同名重绑。Entity 删除保留身份 tombstone；Resolve 仍因客观不存在返回 MissingObject。Facet 的旧引用不得换绑不同实体，即使其旧实体已删除。

新严格查询只返回集合或显式单项状态。旧 `KnownProjection`/`View` 可保持首项兼容但标 `legacy-first`；新 `KnownProjections(soul,entity)` 返回全部已知集合，`KnownFacet(soul,facet)` 精确查询，`LookupSingleKnown` 返回 Unrecognized/Resolved/Ambiguous，绝不取第一项。顺序为稳定句柄 wire 顺序，顺序只用于显示。Entity/Facet 查询只存在于 engine WorldRuntime，不作为主体解释输入。

KnownFacet 的Facet未登记=InvalidReference，已登记但此主体未认识=Unrecognized，已经获知且尚在当前主体图=Success（不因hidden objective Remove改变认识结果）。LookupSingleKnown 同样只按该主体当前已知集合统计，不用客观活跃集合过滤；未知Entity登记=InvalidReference。KnownProjections 的 legacy-like集合签名遇未登记Entity须明确异常而非静默空；已登记但无已知=空集合。主体不认识的Facet不会经此API自动进入认知。

## 3. 原子认知版本与锁

`KnowledgeRevision` 是非负 Int64；空世界为 0，非空成功 Observation 批次恰加 1（包括输入值与现值相同的已接受观察、Forget 及其级联清理）。空批次成功但不加 revision、无审计、无 Changed。失败保持所有认知状态、History、审计、revision 和绑定发布状态不变。revision 溢出在写入前拒绝。

所有主观写入口最终经同一个 ProjectionWorld gate：公开 Observe、公开新版批入口、内部 Apply/ApplyBatch、Cognition.Infer、Corrupt 每次 Observe、WorldRuntime.Observe/Deliver、ActionRuntime 思考与完成观察。Corrupt 的现有 foreach 多次 Observe 仍为多个事务，只有显式 batch 才是一版；本首批不改变它为全组事务。Reality.Sync 属客观视图，不计主体 revision，不能宣称整个 WorldRuntime 跨线程安全。

一批输入仅一次准备→校验→发布。先建立所有新 Projection 和不可变新状态、完整规则/Procedure 图、审计变更、History 结果及绑定增量，确保闭合引用和最终父图无环；完整校验后在 gate 中发布，不在逐输入 Commit 中通知。新 `ObservationChangeV1` 不允许同一 Target 重复，也不允许 Create+Forget。旧 batch 若确有重复 Target，先返回明确 InvalidBatch，不能靠 ToDictionary 偶然异常。关系更新保持现行优先级：Remove 清正/否，Add 覆盖 Deny，Deny 去正并加否。Forget 在批最终图执行现行级联语义：清掉所有子 parent、指向其的正/否关系、任何引用它的 Rule 整条与 Procedure 整体；审计覆盖所有被间接改动的节点。

完整提交后才发一次 Changed。WorldRuntime.Observe 的 `_mental`/`_binding` 发布属于这次观察的 engine publication，必须在 Changed 之前完成。当前源码是在 ApplyBatch 通知后才更新索引，须修正。实现可使用内部 CommitBatch 协作参数传递预校验的绑定增量，不能把任意 public callback 当 commit 扩展点。任何 transform 在正式发布前执行且不持有认知 gate；记录起始 revision，transform 重入变更导致 ExpectedRevision 不同则整个本次观察返回 StaleRevision，不能覆盖新状态。transform 的既有外部副作用不在认知 rollback 范围，API 不承诺其纯净。

gate 外按发布顺序送通知；重入观察可以提交后续版本，回调读取时可看到本版本或后续完整版本，绝不能看到半版。通知携带 PublishedRevision；通知异常属于已提交后的 `NotificationFault`，不得把成功事务伪报成未提交重试。旧 Action Changed 的异常在单独 fault 回执记录并继续其余订阅者；不撤销已提交认知。实现必须说明异常收集，不吞掉它而假称全成功。并发通知按 revision 排队，不让 r+1 通知先于 r。

具体通知签名：`ProjectionWorld.public event Action<CognitionChangedV1>? CognitionChanged`（新入口）与既有 `internal event Action? Changed`（legacy adapter）。同一队列项先按 typed event 订阅顺序调用，再按 legacy event 订阅顺序调用；每订阅者单独捕获异常，队列完全完成r再开始r+1。当前无drainer时提交者在gate外同步drain；回调内重入或已有drainer时，新事务只入队并返回 `CommitReceipt.NotificationStatus=Pending`、NotificationFaults=[]，该空数组不表示通知已成功。无等待，不死锁。已完整送达时Status=Completed、NotificationFaults记录实际异常。提交回执本身immutable，不后来补写；`ReadNotificationReceipt(revision)`取得当前immutable Pending/Completed状态与已完成时fault集合。空批无通知，receipt为Completed/[]；r0查询返回Completed/[]；超出当前版本/负数为InvalidRevision。机制诊断不把异常message中的任意剧情文本分发到subject模型。

`Ast.Evaluate(Condition,ActionContext)` 对主体世界在入口依据 Actor.World 取得一次 gate；验证所有已给 context 投影和 Reference 属同一世界，内部递归 helper 不重新捕获、不逐叶加锁。跨世界输入显式失败；现有未填 Slot 仍产生 Unknown。单次一致保证仅覆盖主体世界。Reality旧入口保留，其读锁不能阻止当前Sync直接改字段，因此不承诺现实AST一致性，也不承诺客观全局线程安全。新版 `Ast.Evaluate(CognitionSnapshotV1,FrozenConditionV1,SnapshotActionContextV1)` 只读 immutable 主体图，不接 WorldRuntime。引用不在 snapshot 中返回 InvalidReference；缺标签/不存在父信息等保持现有 Truth.Unknown 规则，不把 Unknown 变 False。Reality.CaptureSnapshot/AllocateSubjectProjectionId 显式Unsupported，不生成默认主体域。

一次旧 Ast 调用得到一致读；DecisionSystem.Assess 中多次 Ast 调用、Reasoner.Assess、SocialSystem 整轮目前不因此自动一致。首批必须如实声明它们 `LEGACY_MULTI_READ`。下一批应从同一 snapshot 取规则、候选、历史、context 并使用新 Evaluate；不得将所有 leaf 安全等同整轮安全。

旧 public Projection 字段 getter/ProjectionWorld.Projections/History 保留兼容，调用方直接组合它们仍不获得原子整图读保证；只本文有界 API（Snapshot、AST gate、提交）提供该保证。旧 ObservationEvent/History 不作为新版历史正确性证据；Model 内单独维护无源 VisibleHistory 与 engine audit。新 Frozen AST 的 Equivalent overload 逐节点、逐序列内容比较（不能依赖 ImmutableArray 默认相等），引用比较包含 SubjectWorld+Local；不同主体引用不可等同，Slot角色保持，Say.Request不提前绑定，未知节点返回 Unsupported。

## 4. 主体快照与追加审计

`CognitionSnapshotV1` 是完整当前主体图：SubjectWorldId、Revision、VocabularyRevision、ContractVersion、每个节点的稳定句柄、Name、Kind、Existence、Parent、Tags、正/否 Relations、所有 Rule 的 Condition/Mode/Effect/Weight/Acceptance、Procedure、KnownTerms 和主体可见来源。所有数组/字典/集合均 immutable 且不含源 Projection/Essence/WorldRuntime/delegate。SnapshotActionContext 也只含句柄。字符串可来自脚本数据，DTO 不在 Core 写入具体内容。

主体入口 `Soul.World.CaptureSnapshot()` 在该 gate 下捕获。它不含客观 ID/Facet、客观属性、绑定、其他主体、遗忘/已保留句柄登记表或权威 Audit。`VisibleHistory` 是当前仍被保留的、已获知来源记录，不是审计日志。LastObservation 使用无源对象的 `VisibleObservationV1`，含 target/source/time/time_unit/detected/declared existence/details/可见来源位置、显式依赖句柄和完整 `ObservedStateV1`（当时 Name/Kind/Existence/Parent/Tags/正否关系/Rules/Procedure/KnownTerms，无 LastObservation，避免递归）。legacy 来源位置未知可为 null、time_unit=unspecified，不虚构时钟单位或合法性证明。新 batch 显式提供 time_unit，只接受 unspecified/action_value/game_minutes/game_days；跨单位不可直接加减或排序，历史自身的数组是提交顺序而不是依赖 time 数值排序。

Social 可从同一 snapshot 用当前 target 的 Kind 复现旧 Event 筛选、以稳定 Target 分组、排除当前 request，然后取最后 ObservedState.Tags.Trust 与历史 ObservedState.Relations 的 sender；Reasoner 可读当前 claim.Relations 的 sender、LastObservation.Source 与当前 sender Trust。上述提供字段，不在本卡改变评分公式。完整 SubjectVisibleHistory 数据无需 Audit 补充；其 schema 与审计的 before/after 不混同。

`CognitionGraphArchiveV1(SubjectSnapshot,AllocatedIds)` 是 engine 保存元数据 wrapper，保留所有已分配/保留/遗忘句柄以防复用；只能由 WorldRuntime.CaptureCognitionGraph 导出，不分发给主体 planner。把 allocator/tombstone 塞进 public CognitionSnapshot 会泄漏已忘身份集合，所以它不属于主体图字段。Archive 与 Audit 是不同 engine 类型；前者存当前图及ID登记，后者存全部历史原文。

新观察的 Details/来源若包含所指依赖，调用方须在 `SourceDependencies` 记录；legacy adapter 补齐该输入及完整 ObservedState 的 target、parent、正否关系和 AST 引用依赖，但不能声称识别了任意文本语义。Forget 从 VisibleHistory 移除 target 或任意结构依赖涉及遗忘句柄的整条记录；剩余节点 LastObservation 若对应记录被清除，则为 null。不会从审计回填已忘原文。缺少显式依赖的任意自由文本不能作为“彻底语义遗忘”证明；新主观模型不得阅读 legacy 未声明依赖的 Details 作为额外事实。可见来源字段不等于来源真实/可信证明。

`CognitionAuditCommitV1` 是 engine 专属 immutable 追加账本：Revision、前一 Revision、VocabularyRevision 与前一词典版本、所有输入的稳定顺序、完整输入（含 source/time/time_unit/details、可见来源位置）、所有直接及级联目标的 Before/After 完整 FrozenProjectionV1、ForgottenIds tombstone。SubjectWorldId 在 history envelope 中，所有句柄同域。Create 的 Before=null，Forget 的 After=null；其余 Before/After 非空。一版一条 commit。审计留存已忘原文且不被 Forget 删除。任何公开返回值仅包含 DTO；数据权限由不向主体消费方传 WorldRuntime/audit provider 保证，不能宣称 OS ACL 或加密权限。

每条 audit commit 另外完整保存可见历史差分：`RemovedVisibleHistoryIndices`（相对前一版 VisibleHistory 的0-based索引，严格升序、唯一、在范围内）与 `AppendedVisibleHistory`（此次完整提交最终保留的新记录，按输入顺序）。重建历史=按索引剔除旧条目后追加，不依赖 Target 的后来状态或猜测输入生成结果。每个 After.LastObservation 必须能按值对应重建历史中该 Target 的最后记录，或 null；缺值拒绝。此设计避免逐版完整 History 导致二次方保存增长，同时审计仍保留已删除的原记录（前一记录/初始checkpoint已留存）。

审计完整入口在 `WorldRuntime.CaptureCognitionAudit(soul)`，增量入口是 `ReadCognitionAudit(soul,fromRevisionExclusive)`；`ProjectionWorld` 不公开 Audit 字段。必须提供内部初始 r0 checkpoint 与所有 commit，以 `WorldRuntime.ReconstructCognitionAudit(audit,revision)` 还原过去认知；恢复过去版本是 audit purpose，不是当前主体获知。CaptureSnapshot 只导出目前仍记得的图/历史。两套类型和 codec format 不互换。审计重建得出旧 subject snapshot 只交给 engine 权威消费者。

Snapshot 容器与 AST 完整防源引用；把已有 ImmutableArray<Rule> 原样放入 snapshot 不合格，因为 Rule 内 Noun.Reference 指源 Projection。所有转译必须是 closed switch；遇到 custom Condition/Effect/Noun、非法 enum 或不支持来源形态返回 Unsupported，不能 reflection、AnyEffect、类型名激活、默填 null/空数组。

## 5. Closed AST codec v1

格式 `dual-ontology-cognition/v1`，JSON UTF-8 无 BOM；枚举为下表精确小写 wire 名。未知主版本/format/required capability/节点 kind/enum/字段明确拒绝；重复 JSON 字段拒绝；没有 extension bag。所有要求字段必须出现；nullable 字段必须显式 null；可选只有 envelope 的 `required_capabilities`，缺失等于空。浮点 time/duration/delay 必须有限，拒绝 NaN/Infinity；Rule Weight 与整数 tags 完整 Int32，不缩放。数组保持顺序（尤其 Rule、Sequence）；set wire 数组按稳定 key 排序且拒绝重复。禁止 property 名字反射。

| AST | kind | 精确字段 |
|---|---|---|
| Noun.Reference | `reference` | target: SubjectProjectionId |
| Noun.Slot | `slot` | role: actor/target/tool/scene |
| Condition.Constant | `constant` | value: unknown/false/true |
| Exists | `exists` | target: Noun |
| Tagged | `tagged` | target, tag:string, minimum:int |
| Related | `related` | target, relation:string, other:Noun |
| At | `at` | target, space:Noun |
| IsKind | `is_kind` | target, projection_kind: object/space/boundary/body/soul/concept/procedure/event/area/material |
| Not | `not` | operand:Condition |
| All | `all` | items:Condition[] |
| Any | `any` | items:Condition[] |
| Effect.Act | `act` | operation: unlock/travel/take/inspect/destroy/place, target:Noun, tool:Noun/null |
| Set | `set` | target, tag, value:int |
| Change | `change` | target, tag, amount:int |
| TemporarySet | `temporary_set` | target, tag, value, duration:double |
| TemporarySetUntilEnd | `temporary_set_until_end` | target, tag, value |
| Spend | `spend` | target, tag, amount |
| Relate | `relate` | target, relation, other, remove:bool |
| Sequence | `sequence` | items:Effect[] |
| When | `when` | condition:Condition, body:Effect |
| Require | `require` | condition |
| Wait | `wait` | delay:double, body:Effect, until:Condition/null |
| Say | `say` | recipient:Noun, text:string, request:Effect/null |
| Think | `think` | fact:Condition |
| Rule | 无 kind | condition, mode:require/forbid/permit/prefer, effect, weight:int, acceptance:unknown/false/true |

codec 保存全部现存 9 Condition、13 Effect、2 Noun、Rule；不把未执行效果丢失。Effect.Wait 的 delay 与 until 同时原样保存，执行器已有优先 until 行为保持；不借 codec 重新解释。TemporarySet Locked、部分 Think 命题与 Place 的文本解析缺口仍保持既有失败/能力状态：能保存≠能执行/能作者声明。codec 只校验结构、闭合引用、enum 和有限数字，不替物理执行器做能力决策；未来必需能力先进行协商。

v1 engine graph Envelope 精确字段：`format`, `contract_version` (=1), `required_capabilities`, `subject_snapshot`, `allocated_ids`（全部已使用/保留/遗忘 SubjectProjectionId，防再次复用）。subject_snapshot 字段：`contract_version`,`required_capabilities`,`subject_world`,`revision`,`vocabulary_revision`,`projections`,`visible_history`。它不含 format，也不能作为 graph envelope 直接 Decode。FrozenProjection wire 精确字段：`id`,`name`,`kind`,`existence`,`parent`,`tags`（数组 `{tag,value}`）,`relations`/`denied_relations`（数组 `{relation,target}`）,`rules`,`procedure`,`known_terms`（数组 `{term_id,raw_lexeme,canonical_lexeme}`）,`last_observation`。VisibleObservation wire：`target`,`source`（perception/testimony/inference/introspection/corruption/recall）,`time`,`time_unit`,`detected`,`declared_existence`,`details`,`origin`（null 或 `{document_id:string,start_line:int,start_column:int,end_line:int,end_column:int}`，坐标 1-based 且区间有效）,`dependencies`,`observed_state`。observed_state 与 FrozenProjection 的状态字段相同，排除 id/last_observation。当前位置/来源是已知内容数据，不成为实体身份。KnownTerms 集合按 TermId/CanonicalLexeme/RawLexeme 排序、拒绝精确重复；同 TermId 的不同词形不合并，C03 冲突检查另负责。

audit 独立格式 `dual-ontology-cognition-audit/v1`，字段 `format`,`contract_version`,`required_capabilities`,`subject_world`,`initial_snapshot`,`commits`。commit 字段 `previous_revision`,`revision`,`previous_vocabulary_revision`,`vocabulary_revision`,`inputs`,`changes`,`forgotten_ids`,`removed_visible_history_indices`,`appended_visible_history`。input 使用下节 ObservationChangeV1 对应 snake_case 字段；change 为 `{id,before,after}`。从 initial r0 到末版连续且每条 +1；before 与重建状态吻合，after 保持闭合，不允许按最后记录覆盖掩盖损坏。审计 codec 只由 engine API 消费。

Decode 全部解析＋限制检查＋完整引用/父环/跨主体校验后才返回 CognitionGraphArchiveV1，不创建 live 引擎状态。SubjectSnapshot 与 AllocatedIds 必须同域，所有 active/visible-history句柄在分配登记表中，全部历史 references 必须处于当前已知图；audit 的每个历史 checkpoint 则按其自身版本图验证。Decode 在 UTF-8/JSON 出错时 MalformedData。容量限制通过显式 CodecLimits 指定：最大字节、节点、AST 总节点、深度、历史条数；默认分别 64MiB、100000、1000000、256、1000000，超过为 LimitExceeded；这是解码器资源界限，不是游戏容量或压力通过指标。

共同validator适用于所有公开边界，包括手工构造DTO的 Encode/EncodeAudit、Evaluate、Rebind、Fork、RebindCondition/Effect/Rule、ObserveBatch，而非只有Decode：拒绝 null必要值/ImmutableArray.IsDefault/未知子类/非法enum/不闭合/混主体域；可空字段仅按本合同nullable形态允许。输出集合不得IsDefault。Rebind map 的key与Projection.SubjectId一致、全部属于同一个实际ProjectionWorld对象、不能同句柄不同对象、不能跨世界借用；Frozen AST reference必须都在map且同域。非法DTO结构=MalformedData；非法枚举/未知节点=Unsupported；域/引用缺失=InvalidReference；无效身份=InvalidIdentity。语法数值单位不被推测。Typed input重复Target=InvalidBatch。

audit 校验分时点：Inputs引用可用域=本批pre-state的活节点＋本批预分配create，允许引用本批最终Forget的旧句柄；Before只按旧图验证，After按最终新图验证，不能把Inputs/Before全部按最终新图拒绝。Audit历史Before内的旧VisibleObservation仍按其完整当时图验证。`ReadCognitionAudit(fromRevisionExclusive)`须0≤from≤current，from=current可成功返回空增量；负值/超前=InvalidRevision。`ReconstructCognitionAudit(revision)`须在连续r0至末版范围内，负值/超前/不可用版本=InvalidRevision；账本本身有断档/错误before=MalformedData。不得返回nearest checkpoint冒充所需版本。

`Rebind(archive)` 建立 detached 主体图并恢复 AllocatedIds：第一阶段分配所有 Projection 与独立 ProjectionWorld；第二阶段一次性重绑定 Parent/正否关系/全部 AST/Rules/Procedure/可见历史。任何缺引用/非法域拒绝，不由名字找替代、不重新观察现实、不运行 Effects/Changed/UI 回调。保留原 SubjectWorldId 和句柄只用于跨进程恢复；`Fork(snapshot,newSubjectWorldId)` 必须传新的非空域且不等于源域，重新映射当前已知图与可见历史全部句柄并返回完整旧→新 map。Fork 不拿到 retired IDs。Fork 引用 map 仅 subject handles；没有源可变引用、绑定或 objective state。恢复为 detached graph 不新增 revision/审计/通知；调用方发布 session 时另完成 engine bindings。Rebind 的独立世界即使句柄相同也绝不能通过 reference equality 混入另一个 live world。

DetachedProjectionGraph 仅包含新 ProjectionWorld 与 ID→新 Projection 的只读映射；不能直接 Submit，因为尚无 runtime 绑定。提供 `RebindCondition/Effect/Rule` 的显式 map 入口作为后续消费助手；它们检查 map 闭合，不能 fallback 回源 Noun。

## 6. Public API 与错误面

精确类型签名见 [api-v1.cs.txt](api-v1.cs.txt)。`ContractResult<T>` 只有 Success(T)/Failure(ContractError) 两种互斥状态；不能通过 object truthiness 消费。`Truth` 仍原三值，错误不是第四种 Truth。`ContractError` 字段为 Code、Path（DTO 字段或 JSON pointer）、Message（机制诊断，无具体剧情）、UnsupportedKind（可空）、ExpectedRevision/ActualRevision（可空）、CandidateIds（SubjectProjectionId immutable 数组，仅在单项歧义时出现）。

| ErrorCode | 触发 |
|---|---|
| InvalidIdentity | default/空/non-NFC/前后空白/空 Guid |
| DuplicateIdentity | 现有、已保留或 tombstone ID 碰撞 |
| InvalidReference | 缺目标/不闭合/跨世界/现实句柄进入主体图/未保留的新 ID |
| InvalidRevision | 负版本、超前版本或请求的历史版本不可用 |
| StaleRevision | expected 与当前不等；不会写入 |
| InvalidBatch | 重复批 Target、Create+Forget、结构冲突/自父/父环/输入类型不合法 |
| Unsupported | 未知 AST/enum/来源形态/必需能力/版本；不是 Unknown |
| MalformedData | 编码/JSON/必要字段/类型/重复字段/未知字段 |
| LimitExceeded | 显式 codec 限额超出 |
| RevisionOverflow | Int64 递增会溢出 |
| NotificationFault | 发布后的 observer exception，事务已完成（放 CommitReceipt.NotificationFaults/NotificationReceipt，不返回未提交 Failure） |
| Unrecognized | 新单项查询零个已知句柄 |
| Ambiguous | 新单项查询多于一个已知句柄，返回完整候选 |

新入口为 Result；旧入口保持签名并将提交前 Failure 转明确异常，旧 Observation 返回新/原 Projection。旧入口不返回支持不足的假成功。不新增通用 InterpretationResult 到这张卡；C03 将另交付 Unrecognized/Ambiguous/Unknown/Unsupported/InvalidReference/Stale/Resolved 解释状态，不能用上述结果代替解释器已完成。

提交后的身体动作保留原绑定；Forget 与 revision 更新不自动 Cancel，不把新 snapshot 引用转换成原待执行目标。Objective Remove 后继续使用既有 MissingObject。当前 `ActionRuntime.Resolve:14` 对 Actor membership 再验导致忘记 actor 可能失败，是后续执行连续性迁移需测试的静态缺口；本基础 lease 不碰 ActionRuntime，也不据此宣称所有身体目标连续性已通过。Inspect 当前 `:245` 首表现也未改，多切面精确 Inspect 后续必须改其 originating facet 绑定，不能改 WorldRuntime.Representations 顺序暗中达成。

## 7. Provider/consumer 版本与发布闸门

| Provider | 当前版本 | 本基础 v1 提供 | Consumer 与后续责任 |
|---|---|---|---|
| Essence/WorldRuntime 身份 | 引用/无能力版本 | EntityId、FacetId、私有 SubjectProjectionId、严格集合查询 | WorldSession/CoreScriptWorld 新持久构造；新 IR 须显式 IDs；world/v1 legacy 映射仅 opaque，不称新正式 |
| Observation/ProjectionWorld | 无 revision；History 引用 | 原子批、revision、snapshot、engine audit | WorldRuntime 先绑定再 Changed；Cognition/Action/Social 调用自动走同底层；多次调用仍多版 |
| Ast | 每 leaf 活读 | 单调用 gate；Frozen AST snapshot Evaluate | Decision/Reasoner/Social 下一卡整轮快照；第一卡只证明 AST 单次版本 |
| Graph codec | 没有 | cognition/v1、audit/v1、Rebind/Fork | 保存 assembler/主体 planner 只能用相应权限类型；不读取 audit 作为主体历史 |
| WorldScriptDocument | world/v1 + 默认 JSON 未知字段忽略 | 本卡零修改 | H01/C03 后续锁 strict 新正式 IR/capabilities；现在不能对 v1 宣称多 Facet/term 已支持 |
| WorldSession/WorldBehavior | 单表现；菜单 _version | 本卡零修改 | facade 新 `VerifiedFacetPerception` 应一完整来源集合一次观察；`_known` 改 handle集合；choice 记录 cognition/lexicon/capability版本；不能拿菜单 _version 当 revision |
| CoreScriptWorld | string key/legacy constructors | 本卡零修改 | 新定义入口显式 typed IDs/facet；Observe/ObserveRoom 已可收多表现，但 Submit 新句柄须 live bridge 与 revision验证 |
| GameSession | 内存 Continue | 本卡零修改 | 精确 session assembler/发布；Continue Count==1 和 KnownProjection 对 Self 的验证需新 Facet/self合同；不得称现内存交接为持久存档 |

运行能力条目建议冻结：`core.stable-identity/1`、`core.cognition-atomic-batch/1`、`core.cognition-snapshot/1`、`core.cognition-audit/1`、`core.cognition-graph-codec/1`、`core.frozen-ast/1`。此稿只是注册常量/需求名，首批实现可暴露静态 `CognitionContractV1.RequiredCapabilities`；整体 capability provider 与正式 compiler/loader 协商属于 H01，需要另签文件后完成。生产消费方未升级时必须标 legacy/不支持新正式文档，不能 fallback 忽略新字段。

## 8. 本批保存阶段矩阵（强制，尚未实现）

| 交付 | 应保存/恢复 | 此卡状态 | 后续 precise provider 文件与发布要求 |
|---|---|---|---|
| B1 cognition graph | 本文完整主体图/可见来源/已用ID/AST闭合 | 本文冻结候选 | 本首批七文件；只 detached graph |
| B2 engine history | r0＋全commit/tombstone；独立权威权限 | 本文冻结候选 | Model/WorldRuntime/CognitionCodec；不得散发给 planner |
| B3 objective runtime | 实体活跃/tombstone、全部已登记 Property、现实 Facet、绑定、所有主体图 | 本批必做、未冻结/未实现 | WorldRuntime/Model/TemporaryState 等需后续精确租约；未知 Property/Corruption transform 不反射序列化，显式 Unsupported |
| B4 CTB/executions | Now/排序序号、pending intent/continuation、等待条件、body占用、cleanup/临时租约/自主唤醒、取消幂等账本 | 本批必做、未冻结/未实现 | CTBSystem/SimulationSession/ActionRuntime/TemporaryState；当前 ScheduledAction.Action 闭包不可 codec；须定义闭合 typed scheduled tasks 或只在可保存切点明确拒绝任何 pending/lease/agent活动，不能清零 |
| B5 host/session | 章/流程cursor、时钟、进度、随机状态、事件/展示回执、菜单版本、已经提交/送达消费账本及连续会话 | 本批必做、未冻结/未实现 | GameSession/WorldSession/WorldBehavior 与实际章会话尚未授权读取；后续合同逐项登记，不能以这里未审源码填空；解析完整候选新会话后原子替换，失败保留当前 |

最初若选 quiescent-only save：必须对所有 active/waiting/scheduled/lease/autonomous/presentation/flow非切点逐项明确 Unsupported，且通过宿主精确证据界定切点；本稿不自行批准该降级为完整 B。随机状态的 exact-resume 与恢复过去主体再规划分别版本化。重复加载不得重放已提交效果、重跑初态、默填缺失 state；旧章专用快照/partial save event 没迁移器时显式 Unsupported。没有旧档样本及消费确认，不声明旧档兼容。

## 9. 后续独立验证预期（此作者未运行）

须由全新独立上下文 Agent 在批准 lease 的 clone 上执行，不读项目设计：两结构不同中性世界；一次批失败不变且空批不加版；单次成功 N输入仅+1；原/新两个完整态都 False 的 AST 并发不能混成 True；Changed 重入必须看完整图与绑定；Forget 级联审计完整且当前快照/历史无已忘原文；历史 Rule weight/acceptance/Procedure/否关系可重建；codec 全节点回环与跨主体/缺引用/非法enum/未知字段/未来版本拒绝；fork 深隔离；rename/forget/reobserve/new同名/Remove边界；延迟提交后Forget vs Remove，actor遗忘单列未解决；callback fault不重试认知事务。

压力另测种子、规模、操作数、耗时、内存、异常与原始日志。大批次/并发 AST/重复 codec/ID tombstone规模属于中性压力，正式 3–5 日虚拟侦探需正常可见输入闭环另测。不得以中性循环代替正式运行；27项既有正式回归失败保持可追踪。人物与体验未验证，三级不在本合同作者执行范围。

## 10. 证据与披露

直接源码依据：Model:33–54 身份/私有世界、109–165活对象/旧历史、167–243输入/提交/遗忘；WorldRuntime:14–51登记/首项、56–72现实同步、76–129观察与通知先后；Ast:5–50现存类型、69–84求值、99–164映射/引用、168–212closed遍历缺口；Cognition:6–44统一输入与多事务Corrupt；ActionRuntime:14–23 actor检查、118–189支持限制、203–248物理动作、252–287正式commit；SimulationSession:35–49提交与绑定、99–108等待、117–127完成观察、140–173自主回调/取消；CTBSystem:4–66闭包队列；TemporaryState:4–12租约。

宿主直接读取仅授权的 WorldSession.cs、WorldBehavior.cs、CoreScriptWorld.cs、GameSession.cs；WorldBindings.cs Test-Path 得 NOT_FOUND，未寻找/读取替代文件。源码未写路径时为 csharp；宿主为 godot_csharp/scenes/Shared/Story，GameSession为 Shared/Game。本目录访问证据记录 SHA256 和实际读段；hash不证明 Git归属，也不证明共享工作区后来未变。

一次过宽路径发现 `rg --files artifacts/story-hardcode-marking | rg ...` 输出了非授权子目录的文件名并截断；没有读取那些内容，已向 root 披露。若干聚合输出发生截断；关键生产 Model/Ast/WorldRuntime 的截断段和诊断关键合同已精确补读。本轮未读取 docs/任何项目设计/.story/黄金/历史试玩/S18，没有运行 tests、build、游戏、Git 或新建 agent。只写本目录。隔离仅 DETECTION_ONLY，无 ACL，不声称强隔离。原始尝试不会用后续干净读记录覆盖。
