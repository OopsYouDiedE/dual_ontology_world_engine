# F01 只读能力审计

本轮在所审源码中确认了类型化 Core、现实/认知对应、分阶段行动提交、决策与调度。作者接口的实际能力小于 Core API；多切面、关系、锁/通路、复杂认知命题等不能仅凭 Core 类型存在就记为已支持的 `.story` 能力。已有专用快照和部分保存事件也不能记为完整存档。

本报告没有批准任何 Core 增补。逐项审批状态均为 **NOT_FOUND_IN_REVIEWED_SCOPE**：在读取范围内没有找到对应审批记录；本轮没有读取独立审批资料，因而不宣称全仓库没有审批。

## 范围、版本和证据

- 基线：主代理给定 `7489db56a714724eb7cb735958dd879dc662d238`。未执行 Git 命令独立核对。
- 当前范围：前两章约 3–5 游戏日；未来范围：40 游戏日。这是用户范围声明，静态审计未证明时长、容量、性能或最终体验。
- 隔离：**DETECTION_ONLY**。没有子工具硬 ACL，不能宣称强隔离。靠精确授权路径与本任务写入目录限制执行。
- 生产文件、测试、工具、生成物、权限和 Git：本代理零写入。只新建本目录的三份审计文件。
- 设计文件、完整 `.story`、历史试玩、封存黄金/oracle：未读取。未执行编译器、游戏、测试、构建或验收级别一/二/三。
- [capability-manifest.json](capability-manifest.json)：54 项能力。每项包含 Parse→IR→Bind→Execute 函数与行号、类型、执行侧、副作用、失败行为、现有版本、支持度、最小缺口、审批状态。
- [source-evidence.json](source-evidence.json)：40 个授权源码文件的 SHA256、行数和读取段。Hash 用来固定静态证据，不等于独立证明源码属于给定 Git 提交。

核心项目明确使用 `net8.0` / C# `12.0`（`csharp/DualOntologyWorldEngine.Core.csproj:3–4`），没有显式 Core 能力版本。世界 IR 为 `dual-ontology-world-script/v1`（`world_script.py:18`、`WorldSession.cs:42`）。对话 IR 只有 entries，没有 format/version（`engine_spec.py:143`、`InteractionPacket.cs:18–20`）。

主代理追加授权读取编译器模块与指定共享/章节机制源码；具体扩域清单记录在 manifest。最新禁止读取项目设计后，`story_design.py` 未读。`Chapter1InteractionSession.cs` 不存在，文件名索引也未找到，随后按主代理授权读取实际 `Chapter1Session.cs` 与 `Chapter1Flows.cs`。没有未经授权扩读替代实现。

## 已闭合的调用链

| 能力 | 实际链与现状 |
| --- | --- |
| 实体/默认值 | `story_script.Translator.entity:766` → `world_script.Compiler.declare_entity:611 / Entity.json:72` → `WorldSession.Read:254 / DeclarationId:290` → `WorldSession.Load:153`。room/object/character、state/belief/observable/sprite 与新增默认扩展有执行路径。 |
| 身份/切面 | `Essence / Projection / SurfaceProperty`（Model:33/109/87）→ `WorldRuntime.Project:20 / Associate:34` → `Sync:56 / Observe:76`。Core 支持同 Essence 多现实侧面；world/v1 无对应声明，通用宿主又要求一实体一表现。 |
| 条件 AST | `AstDecoder.Parser.Condition:156` → `Condition:16–27` → `ActionContext.Resolve:54 / Ast.Map:99` → `Ast.Evaluate:69`（主观三值）或 `Plan.Evaluate:104`（客观结算）。全部九类条件分别登记在 AST-C01 至 AST-C09。 |
| 类型化效果 | `AstDecoder.Parser.Effect:178` → `Effect:29–47` → `Ast.Bind:131` → `ActionRuntime.Plan.Prepare:118 / Commit:252`。十二类非 Act 效果分别登记；各效果支持度以实际执行分支为准。 |
| 物理行动 | world 编译器 `compile_candidate_action:1033` → `BuildEffect:317` → `SimulationSession.Submit:35` → `Plan.Act:205`。Unlock/Travel/Take/Inspect/Destroy/Place 六项分别登记。Place 缺 AST 文本解码分支。 |
| 认知来源 | `ObservationInput/ObservationSource:143/30` → `Observation.Validate:195` → `Commit:211`。六来源可写；边界/引用/循环有验证，但来源标签本身由调用方选择。 |
| 决策 | reaction 编译 `compile_behavior:732` → `WorldCandidateDefinition:12` → `WorldSession.Assess:200` 临时认知 Rule → `DecisionSystem.Assess:14` → `SimulationSession.Submit / Activate:35/140`。实际评分来自内部世界。 |
| 结构事件/交互 | Core 完成 Event（SimulationSession:121）、typed WorldInput/WorldOutput（WorldBehavior:30–44）、InteractionPacket（InteractionPacket:6–12）及字典 ChapterEvents（ChapterProtocol:17）。存在数种独立协议，没有统一因果事件类型。 |
| 传播/通行 | Core Say→Deliver（ActionRuntime:183 / WorldRuntime:136）；章节 Gossip→Receive（Social:118/212）；动态走廊→RefreshAccess→Passage→Travel（Chapter2Access:23/37 / ActionRuntime:220）。都存在机制，但不构成通用作者广播/通道声明闭环。 |
| 连续会话 | LiveChapterWorld 持原引用（LiveChapterWorld:8）→ BindExisting/AttachExistingContent（WorldSession:100 / Attach:18）→ GameSession.Continue（GameSession:104）。是内存交接。 |
| 快照/反事实/保存 | 第一章 RunReplay 调用边界（Chapter1SessionFlows:737）；第二章 CaptureNight/RestoreNight（NightSnapshot:30/38）；UI Snapshot（Chapter1Session:486）；save 宿主事件（Chapter1SessionFlows:778）。分别登记，未混记为完整保存或通用克隆。 |

上述文件未写路径前缀时，Core 文件位于 `csharp/`；共享机制位于 `godot_csharp/scenes/Shared/Story/`，章机制的完整路径见 manifest。

## 具体缺口与失败/回退点

“最小处理边界”只陈述当前能力缺口应在哪个位置变为可审阅的事实；不授权实现，也不推定剧情需要某个新功能。

| 编号 | 静态证据与实际行为 | 最小处理边界 |
| --- | --- | --- |
| G01 | `WorldSession.ReadProperties:266–285` 只认 space/sprite/state/belief/observable；不认 Surface、Relation、identity、Lock、Passage、Procedure、Rule、Corruption 或临时层。 | 所需声明没有作者→Core 路径，不能以现有 Core 类型冒充已支持；具体扩展需单独审批。 |
| G02 | `WorldSession.BindExisting:115`、`WorldSession.Attach:36`、`WorldBehavior.Process:83–88` 强制每脚本实体恰好一个现实表现。 | 通用桥限制必须列入多切面支持度；无需否定 WorldRuntime 本身已有多切面。 |
| G03 | 编译器 `Entity.json:72–78` 输出 aliases/profile/source_id；`WorldEntityDefinition:12–19` 无这些字段，默认 JSON 反序列化忽略未知字段。world loader 的拒绝列表 `:49–53` 也没有 design/profile/aliases。 | 明确哪些数据由章节 adapter 消费，哪些被通用 loader 丢弃；当前不能声称通用加载已执行这些资料。 |
| G04 | `WorldCandidateDefinition.Intent:20` 被读取，但 `BoundCandidate:48–50` 不保留 Intent；`ValidateCandidatePlan:418–420`、`Assess/React` 不依据它执行。 | testimony/decline 在编译层有白名单（world_script:769–771），通用反应层没有其语义；勿把登记字段当完整能力。 |
| G05 | **确定性保真缺口**：`WorldBehavior.ValidateCandidatePlan:387–390` 复制 line，仅保留 Speaker/Mood/Text/VoiceClip，丢 LineId/SourceId。消费方 `BehaviorCompleted:187–188` 从复制后的 s.LineId/s.SourceId 构造 InteractionLine，故输出为空。上游 `world_script.compile_candidate_response:1001–1002` 明确提供 line_id/source_id。 | 仅补齐已存在字段复制即可保持来源/本地化标识；不需要新增公共语义。当前角色文字未因此改变，定位证据被丢失。 |
| G06 | `Ast.cs:7` 与 `ActionRuntime.Plan.Act:233–243` 有 Place；`AstDecoder.Parser.Effect:182–203` 无 place。 | Place 的 Core/world 编译能力与 AST 文本能力分别标记。 |
| G07 | `infer` 可解析任意 Condition（AstDecoder:202）；`Cognition.Input:8–26` 只支持 Exists/NotExists/Related/NotRelated/IsKind。Tagged、At、Constant、复合命题进入 `Plan.Prepare:179–181` 后 UnsupportedOperation。 | 对每类推理命题分别列支持；不能将全部 Condition 可解析当作全部可提交。 |
| G08 | `Parser.Rules:231` 用默认 Weight=1、Acceptance=Unknown；`DecisionSystem:18` 只认 Acceptance=True。 | 文本 RULE 没有权重/接受状态语法，解析不自动激活。既有接受机制需要调用方另行提交，不默认授权。 |
| G09 | `Ast.Map(Condition):109` 未识别条件原样返回；`CollectNouns(Condition):168–184` 无 default；但两个 Evaluate 遇未知类型抛。Effect Map/CollectNouns 有显式失败。 | 新 C# Condition 子类可能不映射/漏引用边界检查，之后才失败；这属于扩展类边界，不是现有九类条件的失败证明。 |
| G10 | `Plan.Act:210–249` switch 无 default。C# 可构造非法 enum 值，若目标有效/可达，将无动作但成功。已授权脚本路径只产合法 ActionOp。 | 区分直接 C# API 边缘行为与作者白名单路径；不能宣称 .story 可注入非法 enum。 |
| G11 | `Plan.Change:193–203`、Relate `:168–175` 无 Reachable 检查；Act/临时效果/Say 有可达检查。Set Locked 有LockProperty时转bool，无该property时写普通tag。 | 精确记权限边界与类型分流，不依据未读设计断言远程状态操作必错。 |
| G12 | `When:135–137` 只有 True 执行；False/Unknown跳过但阶段可成功。until等待（SimulationSession:99–108）读主观条件，仅靠Soul.World.Changed，无超时；Reality.Sync没有Notify。 | 将静默跳过、认知等待与客观状态变化分开；无观察的现实变化不自动恢复等待。 |
| G13 | `TemporarySet:154/162` 明确不支持 Locked；临时覆盖记录 StateLease，由CTB/动作结束/Cancel清理。 | 临时标签机制已有，临时锁状态不能记支持。 |
| G14 | `Say:186–188` 收件身体无控制灵魂时不发消息但成功；`Deliver:138–149` 无senderView直接return，无法翻译的请求变null而只保留文本。 | “请求被保留”“确已送达”的状态目前没有单独可审阅结果。该行为必须列出。 |
| G15 | `Inspect:245` 选择该 Essence 第一现实表现。 | Core可检查对象，但当前动作不能指定某切面；表现顺序影响结果。 |
| G16 | ActionRuntime `Commit:254–287` 先写客观状态，再Sync/Observation/Deliver；SimulationSession `:90/127` 执行宿主事件回调。异常没有统一rollback或ActionFailure转换。 | 当前“准备失败不提交”只覆盖准备阶段；提交后的回调/观察异常边界须如实报告。 |
| G17 | ObservationEvent（Model:163–165）保存Target引用而非当时Name/Kind/Parent；Projection随观察改名改kind；Forget（Model:214–225）删除关联历史；无版本ID。 | 现有History不是完整不可变认知版本账本。六来源字段并不提供来源合法性证明。 |
| G18 | WorldSession菜单 `_version:60–61` 是宿主令牌版本；没有订阅所有Core认知变更。提交会重新检查Applies（:165），但没有每角色/命题 revision。 | 菜单防陈旧与认知版本分开；不宣称同一功能。 |
| G19 | `DecisionSystem:45–57` 无Permit时默认 Weight=-1、Basis=null；公开Rule构造器允许任意负Weight。若禁令Weight≤-1被此默认项“放行”，访问permit.Basis.Name可能抛。正常解析默认正权重。 | 仅作为直接API未校验边缘输入；未运行重现，不宣称当前生产路径触发。 |
| G20 | `IsCandidateAvailable/Offer→Applies→BuildEffect:108/154/140`，BuildEffect `:338–342` 会创建response marker；AssessReactions `:211–238` 临时观察/遗忘Rule概念。 | 可用性与预览查询并非全无认知副作用；记录命令/查询边界即可，不擅改策略。 |
| G21 | `WorldBehavior.BehaviorCompleted:195–198` 只在option完成后有限React，reaction完成不递归；Core自主Activate另有观察唤醒。 | 这是两种现有机制。通用反应链不等于长期自主生态，未验证40日运行。 |
| G22 | `Reasoner.Assess:74` 对同名Projection取第一；字段直接读tag不查ObservationSource；Weight `:127` 只取LastObservation来源；矛盾时 `:95–101` 选矛盾最少假设；空候选Min会失败。 | 表格式推理支持度与证据来源强度边界须分别记录。没有实例数据/源码答案注入用于本审计。 |
| G23 | 对话 `CompiledDialogueDocument.Parse:18–20` 没版本/严格schema；Literal数组/object→null（:143–144），Truth其它对象→true（:133–136）。章字典助手 `ChapterEvents:31–41` 错类型/缺值静默fallback。 | 当前存在显式未知op失败，也存在类型回退；应按具体入口区分，不能一律说严格类型IR。 |
| G24 | C#任意委托：StoryBindings/StoryStep（StoryRuntime:8–31）、DialogueContext（InteractionPacket:58–61）、CTB.Schedule（CTB:28）、IProjectionTransform（WorldRuntime:8）、configure/Busy/Submitted（CoreScriptWorld:39/57/67）、BlockNews（Social:136）。 | **只是已证C#扩展入口，不是已证.story任意代码执行**。`world_script.parse:194` 用ast.parse；compile_top/static_value白名单拒绝任意Python语句/表达式。原生块也仍交这套编译器。 |
| G25 | 编译器 validate_expr 白名单（compile_story:26–47）允许geo_reach、acquaintance_seen、replay_var、bolted、accused等；第一章流程Eval直接Query（Chapter1Flows:116–118），该Query完整switch（:38–63）无这些名字并明确throw。对话专用Context实现未核验。 | 这是可构造的“编译通过→第一章流程不支持查询”静态路径；不说实际.story已经使用，不把专用对白桥归入此结论。第二章FlowCondition又只支持accused/布尔（Chapter2SessionFlows:35–42）。 |
| G26 | NightSnapshot捕获有限字段（NightSnapshot:30–36）；Restore `:40–56` 只补写/加入，不清除额外标签、公告/开放地点。缺键跳过。无CTB/活动动作/全认知/关系/临时层/新闻等。 | 是专用重建补丁快照，不是Core完整save/clone；未授权依赖调用链继续读的部分标NOT_VERIFIED。 |
| G27 | 第一章 RunReplay只核验调用/结果映射（Chapter1SessionFlows:737–773），未读NightReplay.Run实现或比较数据；save `:778–787` 只发部分payload和内存Publish。 | 通用反事实隔离、文件写入/读档/恢复调度未核验，不能推定全仓缺失或已支持。 |
| G28 | 局部能力白名单分散于compile_story:25–56、world_script:18–43、WorldBehavior:349–423；world IR无required_capabilities/Core最低版本/审批凭证；对话没有格式版本。 | 引擎没有本轮已核验的声明能力目录。F01 manifest是审计产物，不能替代工程实现或批准。 |
| G29 | CTB.Step `:57–59` 先更新时间/出队再回调；RunUntil `:66–68` 达maxSteps仅返回步数，未保证到目标。StoryRuntime Repeat `:183` 非正Count零次；StoryStep switch无default；零可用choice可留pending。 | 分别记录停步/静默零次/未知Step丢弃/等待状态，不能把“无异常”当全流程完成。 |
| G30 | Chapter2Access.HostCanEnter `:14–21` 未列Place默认true；RefreshAccess `:45–52` 在开放地点间重建连接且向全部角色观察；Social.Interpret `:264–268` 通过Event.Details的特定文本识别事实。 | 已有章节路径/传播依赖章节专用schema与文本，不是通用作者通路/结构事件语义；不借设计推导剧情结论。 |

## 未闭合的边界

以下只标 **NOT_VERIFIED**，不宣称全仓不存在：

- `Chapter1Dialogue` 的具体 EvaluateCall/ExecuteCall 映射；本轮只核验 Chapter1Session 的创建与互动调用。
- `Chapter2Narrative / Chapter2InteractionSession` 的 sequence event→trigger→action adapter；通用loader默认拒绝序列，GameSession.Continue显式允许adapter，但具体闭环未读。
- `NightReplay.Run`、其事实比较输入、第二章完整反事实驱动；未读case/oracle内容。
- 宿主的save文件写入/读取、持久codec、完整引用图/调度恢复；GameTrace本身亦未扩读。
- 外部Heard/Claim定义、人物/规则接受的更多专用机制；本轮审已授权Social/Council源码，未把其专用效果升格Core能力。

## 验收结论

| 类别 | 本轮结论 |
| --- | --- |
| 构建 | 未执行；不能给通过结论。 |
| 机制 | 静态能力清单已完成；54项和30个具体缺口/回退点有源码定位。存在已支持、部分支持和未核验边界，不给动态机制通过结论。 |
| 人物行为合理性 | 未验证；未读取设计/完整剧本，不作内容判断。 |
| 游玩体验 | 未验证；未运行真实游戏、UI或盲玩。 |
| 验收级别 | 一级、二级、三级均未执行。本轮仅F01静态能力审计。 |

本代理未提交、推送或修改审批状态。主代理可依据上述证据决定后续研究与已授权修复边界。

