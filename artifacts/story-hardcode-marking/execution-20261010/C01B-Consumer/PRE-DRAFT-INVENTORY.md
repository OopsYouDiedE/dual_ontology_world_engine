# C01/B 消费入口独立盘点（合同草案读取前）

状态：DETECTION_ONLY / PRE_DRAFT_INVENTORY。此稿在读取 C01B-Contract 草案前独立形成，只提出 G-CONTRACT 检查项，不批准合同、生产租约或实现。

主代理提供基线 HEAD `232bb99a...`；本代理不运行 Git，未独立核对提交归属。当前范围是前两章约 3–5 游戏日；40 日仍为未来范围。批准依据是本轮 approval-record.md 与 control/contracts-phase02.md。本轮只静态读取，未运行构建、测试、游戏或一级/二级/三级验收。

## 必需消费入口

| 编号 | 当前入口与定位 | 新合同必须给出的消费行为 |
|---|---|---|
| BC-01 | `Ast.cs:69–84`、`ActionContext:52–63` | 一次顶层 AST 调用先取得一个 immutable 图；所有叶与上下文槽都来自该图，不逐叶截取，不从旧 `Projection` 继续读取。固定的图/主体/世界/版本必须一起校验。 |
| BC-02 | `DecisionSystem.cs:14–68,91–101` | `Assess` 整次调用固定一图：规则列表、Acceptance/Weight、候选遍历、上下文 Parent、Permit、Forbid/When 与评分条件共用版本。只把单次 `Ast.Evaluate` 改为截图仍不足以使多条 AST 的评分一致。返回依据及效果携带同一认知凭证；排序不查现实。 |
| BC-03 | `SocialSystem.cs:11–27` | 收到的请求、sender、Cooperation、Trust、RequestCompatibility、历史分组和排除本请求均从同一主体图。`EvaluateSender` 预览和 `Evaluate` 正式评分共用格式；不能将引擎 audit 当历史传入。 |
| BC-04 | `Reasoner.cs:68–118,121–142` | Fields、Claims、Holds、LastObservation.Source、证词 sender/Trust 与 Confidence 从一个主体版本消费。按名称的 FirstOrDefault 不能被当作新的多义句柄解释器。模型生产来源是独立约束，immutable 参数不证明上游模型没有隐真。 |
| BC-05 | `Ast.cs:99–143,157–212`；`Model.cs:195–206` | Relations/DeniedRelations、Parent、Rule 条件与效果、Procedure、Say.Request 全部按主体句柄二阶段重绑定。槽位仍是槽位；`Say.Request` 的槽位由接收者后续上下文填充，不能提前用发送者填。未知 AST 节点不得 Map 原样透传或 CollectNouns 漏检。 |
| BC-06 | `AstDecoder.cs:15–35,127–147` | 新正式解释固定快照与词典版本；旧文本按名字/嵌入入口的含义与适配策略明确。错误从异常文案升级为机器类型，但不得让解释结果对象或 Truth 枚举通过一般 object truthiness 变 True。 |
| BC-07 | `SimulationSession.cs:35–49,99–108,130–158` | 新决策结果在提交前验证主体、世界、epoch、revision、句柄成员及能力，再交既有执行器。已提交身体动作保留原绑定；更新 revision 或 Forget 不自动改绑或取消。每次 wait_until 唤醒是一次新的完整评估，可使用新的完整版本；单次唤醒不得混叶。 |
| BC-08 | `WorldRuntime.cs:20–73,76–129,136–154` | Entity/Facet 与主体句柄只在绑定层对应。现实 Sync 的内部直接改字段必须明确属于权威现实图，不是主体认知输入。观察批与 binding 发布/Changed 的顺序必须保证重入不见已认知却未绑定的新投影。 |
| BC-09 | `Cognition.cs:30–46`；`Model.cs:167–244` | Observe、ApplyBatch、Infer、Corrupt 都进入 revision/原子提交边界。Corrupt 当前逐对象 Observe；新合同须决定它是多个显式输入还是一个已校验批，不能以方法名推定一次批提交。空批、重复 target、回调异常和 reentry 行为需要固定。 |
| BC-10 | 共享 `WorldSession` 构造/绑定、`CoreScriptWorld`、`LiveChapterWorld` | 当前引用 Registry 与跨章活对象保留是内存连续性。持久 ID、恢复后对象引用、章节 registry 声明 ID、字符 Self/Known 和 Facet 需要明确 adapter；保留旧单切面 API 不得伪称新的多切面作者闭环。 |
| BC-11 | `WorldBehavior.cs:48–69` 与公开方法签名；F01 G18/G20 | 菜单 `_version`、`_serial`、`_choiceTokens` 与认知 revision 分开。预览临时 Observation/Forget 与 response marker 目前有认知副作用（本项 runtime body 只依据 F01 静态报告）；迁移必须明确临时候选是否进入正式 snapshot/history，而不是把任意宿主版本当知识版本。 |

## 历史的两个消费域

旧 `ObservationEvent` 持 live Target/关系 Target，缺当时 Name/Kind/Parent/DeniedRelations/Rules/Procedure；Forget 删相关 History（Model:163–165,214–225）。新审计应保存 immutable before/after 或等价完整变化、revision、时间单位、来源与 tombstone；这些权威历史不能被决策/自动玩家直接调用。

主体消费域必须单独说明其可见历史形态。SocialSystem 现按 Event 身份分组，取历史最后 Trust，并排除当前 request；Reasoner 使用当前 claim 的 LastObservation.Source。仅 `VisibleSource` 一个枚举不能支撑全部社交历史。若 snapshot 没有主体可见 History DTO，必须提供证明等价的 adapter 或保持该项未交付；不能通过分发 audit 补字段。过去快照存在不等于当前主体重新记起已忘事件。跨主体、遗忘对象和 tombstone 的目标引用不得自动恢复为当前认识项。

## 已发现的兼容风险

- `WorldRuntime.Observe:123` 调 ApplyBatch 并发 Changed 后，`:126–127` 才写 `_mental/_binding`。新批提交合同必须明确发布屏障覆盖认知图与执行绑定；否则重入消费方可能看到新图，却不能解析它的现实绑定。此处仅静态风险，本轮没有重现。
- `ActionRuntime.Resolve:14` 激活先检查 Actor 仍在主体世界，可能使已提交动作在 Actor Forget 后失败；获批边界是“遗忘本身不取消身体动作”。目标 Forget 的旧保留行为见 D01A/D01B。本轮不能把仅目标保留推论为所有身体绑定都已合规；需独立 actor/target/tool/scene 与认知效果区分。
- `WorldRuntime.Sync:56–72` 原地更新 Reality；若给所有 ProjectionWorld 通用 revision，必须标记 Realm/Purpose 并覆盖现实写路径，或显式不允许它作为 SubjectiveSnapshot。不得让 Reality 的默认 SubjectId/WorldId 混入主观 API。
- `Ast.Map(Condition):109` 对未知类型原样返回，`CollectNouns(Condition):168–184` 对未知类型无失败分支。新 codec 不能用它们作为未知扩展 AST 安全边界，也不能反射恢复任意 Type。
- 相同显示名字的新实体不是旧 EntityId；相同 Essence 的新 Facet 不是旧 FacetId；Forget 后再认识可创建新主体句柄。保存必须保留 ID 注册器/tombstone 或等价的不复用保证。对象改名应保留 ID。
- 快照 AST 的结构等价性必须按同一主体域的句柄内容与槽位语义判断，不能继续依赖源 Projection 引用相等，也不能只按裸 ID 把两个世界/主体的 AST 混同。
- 异常分类至少区分输入/版本/必要能力/非法引用、stale 凭证、未知命题、执行 MissingObject/NoControl/ConditionFailed/Unsupported。Unknown 不替代错误。旧异常入口的兼容 mapping 应写出；本轮不要求改分数聚合、规则优先级或新增决策器。

## 连续模拟保存依赖清单

认知图 codec 只可标成 B 的基础子交付。完整 save 还须满足以下 provider/consumer；未读的章节内部/随机实现不能依据本表宣布全量。

| 层 | 已见状态或公开依赖 | 完整保存的最低要求 |
|---|---|---|
| 权威现实 | Essence.Properties/Relations；State/Observable、Spatial、Passage、Lock、Surface、Rule/Procedure、Corruption、TemporaryState（Model:33–105） | 各获准属性有白名单 codec；所有实体/Facet 注册和 tombstone；未知属性/transform 拒绝，不反射恢复任意回调。 |
| 引擎绑定 | WorldRuntime `_existing/_binding/_reality/_mental`（14–17） | 保持 objective↔Facet↔mental 的引擎私有映射、受控 Soul/Body 关系；不能按 TrueName 反绑。 |
| 主体认知 | 图、revision、IDs、可见历史、词典/规则/模型/能力版本 | 引用与 DAG/父关系验证，二阶段重绑定；新图只在全校验后发布；旧 ID/恢复 epoch 明确。 |
| 调度与行动 | SimulationSession `_active/_effects/_agents/_wakeups`；CTB 对象、ScheduledAction、Intent/Requirements/Basis、WaitingFor、下一段与 cleanup（21–108） | 活动状态若支持，按获准数据恢复调度/顺序/阶段/幂等与清理；若不支持，精确切点拒绝。不能序列化任意 Action 委托。`_active=0` 不代表无 `_effects` 或 wakeup。 |
| 临时租约 | TemporaryState Layers 顺序、ExpiresAt；ActionRuntime cleanup/expiry（269–283） | 保存取得对象、层顺序、到期时刻、动作生命周期与资源竞争状态，或切点拒绝未清租约；不能丢掉时效层。 |
| 共享世界会话 | Registry/declarations/loaded/order/current；WorldBehavior `_known/_responseMarkers/_choiceTokens/_executions/_presentations/_outputs/_version/_serial` | 绑定与候选声明来源固定；持久状态与仅进程令牌生命周期分开；重复载入不重发已完成 effect/回执。 |
| 流程与对话 | StoryRuntime `_started/_fibers/_choosing`；Fiber Steps/Execution/Children/Choice；CompiledDialogueRuntime `_frames/_choices/_pending` | 定义可保存指令游标和待回执/choice；无 codec 的委托型流程只能精确拒绝相应切点，不能清空或从头重跑。 |
| 连续游戏 | GameSession `_session/_waitingForView/_started`；LiveChapterWorld.Progress；ChapterSession rooms/progress/observations | 章节/流程及 view-ready 握手与呈现账本；内存 Publish 不充存档。ChapterSession object 值需白名单类型，不一般 object 序列化。 |
| 随机与规划 | D04 报告的 planner Completed/Feedback/VisibleText/RNG 和 model-source/cache | 精确续演需随机状态与算法版本；不以同种子但不同消费次数代替状态恢复。本轮未读 planner/章节实现，保持 NOT_VERIFIED。 |

静止切点的判定必须由上述全部 provider 共同给出；不得把“无当前 action”当作任意回调、时效租约、UI回执和流程都静止。失败加载必须保留当前会话，完整解析/校验/建图在隔离候选会话完成后一次发布；重复载入不得重复提交。

## 草案到达后的 G-CONTRACT 核对项

1. 逐字段核对四类 ID、世界/主体/恢复 epoch 作用域与不复用语义；Snapshot 是否能表达完整图与 SubjectVisibleHistory。
2. 固定评分级 read scope 和所有 AST adapter；请求/Rule/Procedure/Wait 的绑定时点与结果凭证。
3. 写入入口、Reality Sync、Changed 时序与 reentry、失败批与 revision 的覆盖矩阵。
4. wire format 主/次版本、必要能力、未知字段/AST/属性、各错误类别及写前拒绝行为。
5. codec 的 exact-scope、二阶段恢复和不能称完整 save 的明确项；后续保存卡仍依 AR 本批约束。
6. 原有 API 各自保留/adapter/显式拒绝，不以 silent ignore 声称向后兼容。

## 取证与失败披露

读取限定：根/子 AGENTS（不沿 docs 链接），批准/phase02、F01/D01A/D01B/D02/D04/R01-ContractsPreparation 报告；11 个授权 Core 文件；Shared/Story 与 Shared/Game/GameSession 的接口/构造/绑定段与签名。未读 docs、.story、编译剧情 JSON、黄金、S18、历史试玩或章节剧情实现。

`read-evidence-initial.json`、`read-evidence.jsonl` 保存 SHA256、字节数与输出范围。源码行数盘点以及 slice/hash 使用 PowerShell 装入完整文件字节，再仅输出所列片段；没有硬 ACL。报告聚合首次输出被工具截断，随后分文件补读关键报告；不将截断作为产品失败。首轮误猜 `godot_csharp/scripts/Shared/Story`/`godot_csharp/Shared/Story` 未找到，随后仅目录名索引找到 `scenes/Shared/Story`。首个 slice helper 的单范围数组被 PowerShell 展平，Attach/Live 没有输出，已单文件重读补齐；第一次 evidence 的 ranges 原样保留以披露该取证错误。

本代理没有清除任何既有失败；批准记录中的原 27 项回归失败仅作已报告状态，未读原日志、未复跑，不宣称已解决。本轮构建、动态机制、人物行为合理性、游玩体验以及一级/二级/三级均未验证。
