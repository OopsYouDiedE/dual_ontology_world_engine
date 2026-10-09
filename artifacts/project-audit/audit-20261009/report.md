# 测试证据、剧情边界与评估代码审计

审计日期：2026-10-09，Asia/Shanghai。基线提交：`4aca6644449cd00ba5f5521ad086af54b31dd225`，提交时间 **2026-10-09 18:34:30 +08:00**，分支 `claude/blind-main-20261009`。检查的是本工作区及该提交可见历史，不能代表其他机器或未提供的私人记忆。

**结论：测试产物有明确的不同用途，不能合并成“游戏已验收”；Godot 章节实现和独立 Core 中均有手写专案剧情逻辑；现行统一回归只接入六个步骤，大量其他检查、采集器与历史脚本并未纳入默认回归。现有证据不足以宣称当前版本整体通过。**

本轮为静态审计：没有运行构建、游戏、一级/二级/三级验收，没有修复产品。初始三份 `generated_lines_zh.{scene,speaker,zh}.translation` 的既有暂存和工作区改动均保留。独立子代理只读代码、规则与证据，没有读取设计文件；主代理读取现行设计规范核对边界。

## 方法与完整清单

- 先用文件枚举和正则定位，再阅读条件、返回值、调用与项目编译范围。扫描 **316 个 `.cs/.gd/.py/.csproj` 文件**，四类正则产生 **5,809 个候选行、涉及 160 个文件**。这些是候选命中，**不是 5,809 个违规点**；同一行可命中多类，中文错误消息、注释、生成物、测试夹具均可能命中。
- 三个全新上下文 `gpt-6-luna` 子代理分别复核 Core/Shared/Host/UI、两章实现、评估代码。代码审计明确区分通用演算、由 `.story` 编译的数据、手写专案条件/文案、测试夹具和编辑器样例。
- 原始目录清单：`artifact-files.csv`；各文件包含 Git 首次可见/最后变更时间、文件系统时间与文本时间候选。`inventory-summary.json` 汇总目录；`regex-candidates.csv` 保留逐文件逐行定位；`inventory.py` 可重做静态清点。
- `report-evidence-presence.csv` 检查了一组选定报告引用的原始材料是否实际存在，非所有引用的穷举。`evidence-integrity.json` 是本轮对既有盲玩副本与录像的只读哈希核对。
- 分片报告：`agent-core-shared.md`、`agent-chapter-logic.md`、`agent-evaluation-code.md`。评估逐入口及辅助文件清单另见 `agent-evaluation-entrypoints.csv` 与对应补充报告。

## 1. artifacts 中究竟是什么

本轮输出排除后，磁盘上共有 **1,089 个成果文件**，其中 **1,034 个在基线提交中受跟踪**；另外 55 个属于本地未跟踪/忽略文件。这是文件数，不是测试次数。排除了 `bin/obj/__pycache__`。同一盲玩原始目录与封存副本各有 345 个文件，本轮逐文件 SHA-256 核对完全一致，因此不能算两次独立测试。

时间必须分清：文件系统修改时间可来自复制、切换分支或导入；Git 时间表示写入版本历史；只有运行元数据、日志或明确的报告正文能支持生成/执行时间。以下将二者分别写明，正文只给日期时不虚构具体时刻。目录最晚 Git 时间也不能自动代表其中每个文件的生成时间。

| 目录/数量 | 实际用途与时间证据 | 能说明什么、不能说明什么 |
| --- | --- | --- |
| `basic-ui-system/`，648 文件 | 10-08 的一级源路线→引擎连续路线→正式 UI 回放；5 轮文学审评及技术记录。另有 4 轮短剧情 UI 选段、布局草图、录制预检与三级会话。目录内文件最晚入 Git 是 10-09 01:55:43。 | 第5轮记录报告 1546 包、371 条对白的渲染对应及文学通过；二级末轮 18 张图、两种视口的选段显示通过。均只对应冻结版本/选定路线或样本。02 驱动预置答题方案，03 回放02，所以不构成自主盲玩，也不证明碰撞或有效游玩时长。 |
| `basic-ui-system/level3-6_1-sol/level3-sol61-20261008-01/` | `recording.json` 的 epoch 显示 10-08 21:35:51 开始、10-09 01:37:46 结束；会话元数据 `started_utc` 与此相符。10-09 核对报告、340 张截图、输入/回执、录像、封存副本。 | 三级 **未完成**：停在调查，未首次重演、答辩或终局；输入与随后场景不一致，根因未定。记录 339 条指令、338 条回执，序号133回执缺失。约4小时录像包含模型和工具等待，不能当有效游玩时长。本轮视频哈希与历史报告一致，但没有重新观看或解码整片。 |
| `remake-belief-replay/`，368 文件 | 345 文件是同一三级会话的原始目录；4 个录制预检各4文件；6份新任务/交付文件；1份旧回归修复报告。盲玩元数据时间如上。 | 预检只证明驱动/窗口/录制准备；不是剧情测试。回归修复报告记载末轮 129 项中1失败、第二章入口当时失效、冒烟断言通过但有 ERROR。其所引部分原始日志在当前目录缺失。 |
| `remake-belief-replay/codex-tasks/` | 10-09 的流程/菜单和正午撬门任务、交付报告；最后一份随基线在18:34:30入 Git。 | 最新交付主要是编译/生成一致性记录，明确没有实机或独立验收；旧的一级/二级/三级结论不能自动延伸到这些新改动。 |
| `ch1-actors/`，8 文件 | 报告正文日期10-08，10-09 04:29:13入 Git。含真实碰撞的 GDScript 夹具、反射回执 C# 夹具和源清单。报告称21项碰撞、25项回执通过。 | 是走位、取消、释放等机制验证，合成空间/注入回执不属于游玩。当前目录未见报告提及的主要原始运行日志，报告记载与直接可复核证据分开。 |
| `ch1-session-logic/`，1 文件 | 独立语义/协议报告，10-09 04:33:30入 Git；正文记录连续会话、取消、重演变量与失败分支。 | 报告称目标协议检查通过；默认构建/统一入口失败，旧 `recall_offer_su` 检查失败，UI探针有字体/依赖错误。当前只保存报告，相关输入、日志和临时夹具不在该目录。 |
| `ch1-staging/`，9 文件 | 10-08 定向走位/场面测试，10-09 04:36:06入 Git；两份报告和7张导航/动画图。 | 报告称六个连续协议场景、13段导航通过；回执和观察位置由夹具构造。图尚在，原始协议 JSONL/构建日志缺失；不是完整剧情或盲玩。 |
| `ch1-strings/`，2 文件 | 文案迁移验证报告及路线说明，10-09 04:40:45入 Git。 | 报告记载216组重演/1966帧等价、文案变体不改变逻辑；也记载默认 host 失败、实际 Godot 加载失败。比较 JSONL、测量 JSON、原始日志没有保存在当前该目录，因此这轮审计不能重做其等价性结论。 |
| `ch2-flowcheck/`，1 文件 | 10-08 自动夹具检查报告，10-09 05:18:01入 Git。 | 报告末轮104项、103通过、1失败，且切 UI 时 viewport 空引用 ERROR。跳状态、合成输入与地图扫查不属于盲玩；失败后另有修复提交，不能据旧报告断言今天同一故障仍在。round-05原始目录不在当前工作区。 |
| `ch1-portrait/`，1 文件 | 立绘资产QA报告，10-09 08:41:50入 Git。 | 报告称六表情纹理、头像控件渲染与alpha边界通过，另记录留白规格小差异及默认构建失败。当前没有所链接的两张渲染图/测量JSON，不能将报告文字视作本轮看过图片。 |
| `ch1-locker/`，2 文件 | 交付报告及1张图，10-09 08:55:13入 Git。 | 报告记载二级专项61项、20图通过及flow129项1失败；当前不见 `ui-v05` 全集、evidence.json、final-v05报告/日志，不能直接复核这20图和全部断言。 |
| `project-audit/verify-logs/`，18 日志 | 本地未入基线；目录名为10-08 14:30:53、18:28:39、10-09 01:48:28，由 `datetime.now()` 产生，不带时区。 | 前两轮仅有core/scope/host日志；最新仅core/scope/flow，scope输出24通过，flow开场失败并连续Nil错误。没有该轮flow2/gallery；这是早于18:34基线的失败记录。不要把文件复制时间当执行时间，也不要当本轮新测试。 |
| `chapter-one-ecology/`，1 文件 | 自动模拟动作时间线，10-07 15:11:29入 Git；文首明确“到场代报、未验几何、非实机盲玩”。 | 用于查看生态/行为与调度输出；1961条整场记录不是1961条测试，也没有物理空间验收意义。 |
| `remake-handoff/`，3 文件 | 10-07～10-08的交接、修订计划和多日评议。 | 历史状态/任务依据，含旧失败和待办；不是当前运行结果。“SharedRuntimeCheck崩溃”等需要结合后续代码与新运行判断。 |
| `design-history/`，7 文件；`pixel-cast/`，19 PNG；`releases/`，1 nupkg | 设计历史10-08 17:05入 Git；美术参考/展示10-07 21:38入 Git；本地Core包未跟踪。 | 分别为考据、资产和交付物，均不能直接计作游戏测试。 |

重要区别：早期失败被保留、替代构建与原命令失败分开、三级不通关如实报告，是已有证据纪律；但报告所称“日志全部保留”不保证材料随工作树迁移/提交到了当前机器。当前证据缺口不证明当初没测，只限制今天可以核实的程度。

## 2. Core 与 Godot 是否有剧情逻辑

**都有。** “让通用运行器执行外部剧本”合理；“某角色、某证物、某答案的条件直接写在手写 C#/GDScript”是另一回事。主代理对照 `docs/design-standard.md:1085,1101,1113`：独立Core不出现专案名词；`.story`是作者源；纯C#章节会话可执行条件/文字/流程。这不等于允许会话另写一份剧情来源。

| 范围 | 可复核实例 | 编译与可达性结论 |
| --- | --- | --- |
| 独立 `csharp/Core` | `HallCausalityRules.cs:42` 起固定门向、木楔、烟、双锁、包纸、擦拭带的解释与因果模型；`HallReplayProgram.cs:12` 起固定烟事件、音乐家到隔离门的指令/分支/中文输出。 | 两文件均由Core默认源码包含。静态引用只找到Hall类之间调用，没有找到正式游戏外部调用方。因此“Core源码及程序集包含”确定，“当前玩家运行会经过”未证实。`csharp/AGENTS.md`虽称其不关具体剧情台词，实际代码仍有专案世界知识；仅看规范描述会漏检。 |
| 第一章纯C#章节程序集 | `Night/MorningTraces.cs:79` 的E编号→专案事实字段映射；`Night/NightReplay.cs` 固定四人/房间/案件事实模型；`Script/Chapter1SessionFlows.cs:373,592,812` 的E11/E3特例、声音前置和阶段触发。 | `Chapter1Script.csproj`明确纳入手写Script与 `../Night/*.cs`，GameSession正式引用。专案逻辑确定；不是仅存于历史样例。TRUTH和多数显示文字已经从生成数据读取，不能据此说整章全靠硬编码。 |
| 第二章純C#章节程序集 | `Prototype/Case.cs:79`按Quer/Margaret/Mora、规则接受、病情和晚餐改重演世界；`Script/Chapter2Script.cs:199,245,344`固定实体、角色钥匙/认知；`Prototype/Player.cs:323`碑文/角色条件和手写剧情反馈，`:468,534`证词/物件文本；`Council.cs`固定议事条件与后果。 | `Chapter2Script.csproj:17`纳入 `../Prototype/*.cs`，只排除Program.cs；GameSession正式引用。`Chapter2InteractionSession.cs:260,525`调用RunSlot与ReplayCase，因此专案模型有正式接入。拓碑具体分支是否对当前玩家开放未运行证实，设计本身也注明第二章不可达；不能因文件在Prototype目录就当成不进产品。 |
| 生成与样例 | `Chapter1CompiledDialogue.cs`、generated JSON；UI的Editor Preview对白/笔记；工具/检查里的固定答案。 | 从 `.story`生成的C#/JSON是派生内容，单凭出现剧情不能判手写违规。编辑器样例与正式内容分开。PlayerTranscript固定作答方案是验收驱动而非生产剧情，也因此不能算盲玩。 |

文字之外的证物映射、场景初始化、特定人物因果条件同样是剧情/谜题/设定逻辑。本轮只确认存在和来源边界，没有枚举所有违规行、评价每个人物正确性、证明每个分支可达，或执行迁移。

## 3. 当前评估体系有什么

现行默认入口是 `tools/verify.py`，不是“仓库里所有检查自动都会运行”。其六步如下：

| 步骤 | 实际内容 | 判断性质 |
| --- | --- | --- |
| core | 构建 `csharp/ScopeRegression.csproj -c Release` | 编译/退出码，不是运行全部Core基准 |
| scope | 运行ScopeRegression | 人工构造状态的24项作用域机制断言 |
| host | 构建Godot解决方案 | 构建/退出码；项目自身也有剧本生成一致性门槛 |
| flow | Awakening/check/flow_check.gd | 注入状态、debug跳转与合成输入的自动机制回归 |
| flow2 | Chapter2/check/check_flow.gd | 注入/合成输入的跨章与第二章回归 |
| gallery | Awakening/check/state_gallery.gd | 自动状态截图采集和有限断言，需要实际阅图才能判断视觉 |

默认首个失败即停止；`--keep-going`才能继续。Godot步骤另外扫描SCRIPT ERROR、Parse Error、加载失败、ERROR和泄漏等文本；退出0不自动等于通过。Scope、host等步骤依赖进程退出码。

逐文件清单共 **74 条代码/项目/辅助记录**，覆盖入口、项目、辅助夹具、采集器、解析器和历史脚本；这是评估体系的静态清单，不是74个独立测试或74个正在运行的入口。四份只有报告、没有对应代码的历史诊断已从代码清单排除，另在成果证据章节说明。其他代码按功能应分开清点：

- **12个独立C#检查项目**：Core的ScopeRegression、ReasonerRegression；第一章ReplayCheck、StoryPacketCheck、FormalScriptCheck、SessionCheck、CognitionCheck、LawCheck、SharedRuntimeCheck、VariantCheck；共享WorldScriptCheck；第二章InvestigationCheck。另有DetectiveSimulation等模拟器，PlayerTranscript等证据生成器，不混计为检查项目。除Scope外没有全部纳入默认verify。
- **Godot专项检查与夹具**：完整逐文件表列flow、flow2、gallery、smoke、room fog、UI modules/prefab、柜架、手轮廓、audio、transition、预览等，以及chapter1/chapter2 fixture、ChapterProbe、autoplay等辅助。`final_shared_runtime_check.gd`与`hand_outline_check.gd`仍载入旧 `Main2D.tscn`；该场景存在但规约标为旧预览，不能替代正式Main验证。
- **Python**：六个 `tools/check/test_*.py` 单元/静态测试；perturb/chaos两类模拟；`compile_story.py --check`检查源与七项生成物一致，两个章节项目在构建前调用；`art_direction_mcp/check.py`及资产/结构工具按用途另列。生成一致不证明设计和实现合理。
- **一级证据链**：PlayerTranscript → capture_player_transcript.gd → export_ui_transcript.py；01由读源整理，没有生成工具。02使用正式连续会话，但预置正确答辩文本/证据和重演变量；03将02交给实际UI回放。导出器检查终局实际画出，采集/导出器不等于文学评审。
- **二级选段/布局工具**：capture_story_ui_samples.gd、capture_ui_start.gd、draw_player_transcript_layout.py等。采集器生成图片，阅图与结论另需独立评审。
- **三级驱动与分析**：playtest_session.gd、blind_playtest.py、player_trace.py、record_blind_session.py、summarize_blind_playtest.py。分别提供真实输入、留痕、录制与解析；它们不能自动裁定人物/推理/文学/体验是否合格。
- **历史成果内脚本**：actor_motion_regression.gd、StageFailureFixture、finalize_verification.py、文学各轮coverage/temporal脚本、capture-dialogue.py等。与各自输入/版本绑定，不能因在artifacts而成为当前全局回归。

明确的低质量断言实例：`Awakening/check/SessionCheck/SessionCheckProgram.cs:103`的手稿断言尾部有 **`|| true`**，永远成立。它削弱的是这一条断言，不能据此说整个SessionCheck或所有测试都无效。历史文案验证报告已承认该限制，但代码仍留着。

本轮结论按四类分别是：构建未新验证；机制源码及历史证据已审计但当前回归未运行；人物行为合理性未作新文学/设计验收；游玩体验未新盲玩。一级、二级、三级均未新执行。历史SharedRuntimeCheck崩溃、第二章旧失败数字，不能当今天的运行结果；只有重新执行相应检查才能确定目前还剩什么。

另外，静态核对发现部分旧入口的资源已不存在：`final_content_check.gd` 引用的AwakeningEngine.cs/AwakeningContent.gd、第二章旧截图脚本引用的Chapter2Game.tscn，以及早餐录制脚本引用的record_daily_clip.gd。它们是仍留在树里的评估代码，不应直接计入当前有效覆盖。逐入口表保留原始Git时区，时间跨表比较须转换；主报告与成果目录清单已统一为+08:00。

## 4. AGENTS.md 怎样成为真正的约束

当前代码就展示了：说明文件写得严格，并不自动保证执行严格。需要把可机器判定的要求交给独立于生成代理的门槛，例如Core专案依赖/词汇与编译边界、手写剧情候选、生成物一致性、禁止恒真断言，以及提交与证据的源版本绑定。正则做筛查，复核确认含义；通过改名称躲过正则仍可能存在专案模型，因此不能称“AI绝对无法偷懒”。

设计、人物、推理公平等要求需要可追踪的设计条目、独立上下文评审和真实游玩证据。生成代理不能自己改验收标准后给自己发通过；规则或测试改动要单独审查。报告中的通过应链接原始日志/输入、源哈希、执行版本，并明确覆盖范围；缺证据、过期或未运行均不能自动转成通过。本轮仅提出审计结论，未实施这些工作流程变更。

## 5. 追加配置变更

已修改 `C:\Users\iii\.codex\config.toml`：新增 `[agents]`，设置 `max_concurrent_threads_per_session = 7`。用Python3.12 TOML解析器检查修改前后其余值完全一致；原字节备份为 `config.toml.before-subagents-7-20261009.bak`。没有将全局配置/备份复制进仓库，避免包含无关私人配置。

当前CLI为0.159.2；官方配置参考将这个键定义为每会话并发子代理上限、**不包括主代理**，旧 `max_threads` 是其别名：[官方配置参考](https://learn.chatgpt.com/docs/config-file/config-reference)。本会话工具仍声明4个总并发槽位，不能宣称新值已经热更新；后续新会话应按新配置分配，本轮没有重启应用。
