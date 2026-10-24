# DV 会话与双宿主接口

`DvSession` 持有唯一权威会话，Godot 和 `dv-cli` 共用同一 Prepare → Execute → Complete。生产内容由 `../modules/60-game.dv` 组合，基础机制由 `../base/DuelView.Core.csproj` 提供；Godot 依赖停留在游戏适配层。轨迹工具及 oracle 位于 `../checks`，没有进入生产程序集。

## 接口

会话性能与轨迹来源从正式 `run_config.simulation_mode` 读取策略。当前生产记录 `approximate-v1` 和近似标记；缺少模式的历史状态记为 `unrecorded`，近似标记留空。已退役的 `selective_planning` 与 taskgraph 不再决定元数据，报告不把新策略标为完整搜索。

| 接口 | 用途 |
| --- | --- |
| `OpenConfiguredCampaign(options)` | 接受 `controller`、`viewer`、`campaign`、`audit`、`developer` 开局配置 |
| `OpenConfiguredCampaign(options, diagnosticOutput)`、`DiagnosticOutput` | 配置初始化及正式求值的诊断输出；普通 TUI 使用空输出，诊断不混入玩家正文 |
| `PrepareValue(selection)`、`Intent` | 通过 DV 重核候选，返回当前意图的只读 .NET 值图 |
| `CompleteValue(receipt, viewer)` | 接收现场回执、结算权威效果、返回观察者呈现与真实 `outcome` |
| `Project(viewer, forceFull)` | 返回 `HostStateDelta`，含版本、基版本、重置标志、变化字段和删除字段 |
| `Materialize(viewer)` | 返回同版本缓存的完整只读投影，供初次安装及文本兼容接口读取 |
| `ExecutorProjection(actor)` | 执行者与观察者相同时复用；其他执行者只投影现场所需的数据 |
| `PrepareAhead(selection)`、`TryAdoptPrepared(token, out intent)` | 提前计算候选分支；正式选择在原状态版本仍有效时采用 |
| `CancelPrepared()` | 丢弃候选分支，保持正式状态与费用 |
| `PrepareBackgroundValue()` | 在人工等待边界调度同刻 NPC，沿唯一 pending/Complete 闭环执行 |
| `SimulationPulse(seconds, visibleRooms)` | 在空闲已提交边界传入累计单调现实秒与可见逻辑房间，更新1Hz近场范围；恢复后按实例重定现实秒基点，游戏分钟由正式行动推进 |
| `TextStatus(viewer)` | 指定观察者的位置、同房人物、近期实际观察及输入状态；无参数时使用会话观察者 |
| `Actors`、`ObserverOptions(viewer, includeImmediateScores=false)` | 轻量角色目录、指定角色当前合法候选及已记录评分；显式请求才计算即时分；不构造全体认知地图 |
| `Preview(viewer, includeObjective=false, includeImmediateScores=false)` | 只投影请求角色的主观地图；显式请求时附同 Version 共享的客观地图，保持认知不变 |
| `IsScheduled(actor, actionId)` | 只读核对该人物的选择是否仍在调度队列，不推进状态 |
| `RecordInteraction(payload)` | 记录 focus、confirm、result、cancel，与正式事件保持因果次序 |
| `Inspect(actor)`、`ExecuteDebug(command, argument)` | 显式开发会话中的双状态查看、位置及时间命令 |
| `Close()` | 追加一次关闭事件并缓存已封存轨迹，重复调用保持结果 |
| `Trace()`、`WriteTrace(path)` | 读取当前或已关闭的轨迹，不关闭会话 |
| `Save()`、`WriteSave(path)` | 保存最近已提交的稳定状态 |
| `Restore(value)`、`RestoreFile(path)` | 恢复稳定会话，保留审计和开发模式 |
| `PerformanceSummary()`、`PerformanceSnapshot()` | 只读轻量摘要及完整分项/小时汇总，不推进会话 |
| `WritePerformanceReport(prefix)` | 导出中文报告及派生 CSV；关闭时封存对应正式二进制流，其他新前缀导出明确标记的尾部快照 |
| `SetPerformanceController(kind)` | 首次操作前设置 engine/human/llm 诊断来源，须符合真实控制权 |
| `BeginDecisionInterface(actor, phase)`、`SetDecisionInterfacePhase(actor, phase)`、`EndDecisionInterface(actor, outcome)` | 外部请求实际交付、已观测阶段切换、接受或取消边界 |
| `StartPerformanceRecording(prefix)` | 为会话开启唯一缓冲二进制原始采样流，保留完整实际计算、决策与接口记录 |
| `SetPerformanceProvenance(value)`、`SetPerformanceLifecycle(value)` | 发布实际宿主来源及 trace/durable 动作生命周期边界 |
| `RecordPerformancePlanning(actor, eventId, work, stopReason)` | 在正式新增选择事件边界累计本次实际规划工作 |
| `StopPerformanceClock()` | 幂等停止现实游玩时长，不修改世界或关闭会话；Close 自动调用 |

`Prepare`、`Complete`、`Snapshot` 继续提供进程内 JSON 交换。正式 Godot 路径使用值图与增量接口，省去 JSON 编码和重新解析。文本协议的 JSONL 用于 stdin/stdout；持久轨迹保持二进制格式。

## 引擎统计

用户要求的每游戏小时折算使用本次实际累计 `advanced_game_minutes / 60` 作分母：`engine_decisions_per_game_hour` 为 engine 来源新增选择数，`engine_decision_compute_ms_per_game_hour` 为选策计算毫秒，`elapsed_real_ms_per_game_hour` 为实测会话现实毫秒；零推进保持缺失。`engine_decision_compute_total_ms` 来自紧邻 `dvs_choose` 的 `decision-compute` 阶段实际墙钟，含规划及未采用 speculation，扣除性能流编码/写入和外部 `PhaseObserver` 回调，避免把整段接口、其余认知、Complete、投影、等待或归档当作思考；它不是纯 CPU 时间。活动引擎操作中不生成混合截止的折算。未闭合或失败的计算区间单列 `engine_decision_compute_incomplete_calls`，使总计算耗时及耗时折算保持未知；`engine_decision_compute_recorded_ms` 只保留有效闭合区间之和，`per_game_hour_measurement_complete` 明确完整性。内部计时阶段不转发给外部 PhaseObserver，既有阶段观察仍保留并单次扣除其真实回调耗时。新增全局引擎折算只在自主摘要及完整工程 Snapshot 提供。二进制 `performance-sample.decision_compute_ticks` 只在有效闭合的选策计算区间记录扣除后的实际ticks，其他或异常区间为空；原 inclusive `duration_ticks` 保留，二者不混用。


`dv-performance-v3` 的三个主指标统一使用现实游玩小时作分母：`game_hours_per_real_hour` 表示游戏小时/现实小时，`decisions_per_real_hour` 表示正式选择次/现实小时，`interface_hours_per_real_hour` 表示当前控制来源累计接口小时/现实小时。主界面把上述每现实小时速率与用户要求的每游戏小时选择数量、选策计算耗时和现实经过时长分行列出，明确两组分母与计时范围；详细视图另附真实采样时长与游戏推进量，不把接口耗时当作选策计算耗时。零现实分母为缺失；现实时间有效而游戏未推进时速度为 0。短窗口比率不是完整现实小时实测或整局预测。

现实游玩从打开／恢复完成至关闭请求，包含阅读、思考、输入等待、执行、投影和引擎处理；初始化与关闭归档单列。引擎接口累计正式计算加提前准备的排他墙钟，包括未采用的实际提前计算。人工或模型接口从真实选择请求交付开始，到正式接受、取消或关闭为止；没有请求记录时保持缺失，不用整段会话时间替代。

宿主在选项真正可见后调用 `BeginDecisionInterface`，同一主体重绘不重启计时；实际收到输入或模型响应时调用 `SetDecisionInterfacePhase`。只有宿主报告的阶段才记录，网络、排队、模型内部推理不作推测。人类/模型的正式 `action_selected` 自动写 accepted 和事件引用；取消使用 `EndDecisionInterface`，关闭记录未完成区间。外部等待可与 NPC 引擎工作重叠，来源耗时不可直接相加当作整段现实时间。

决策仅以本会话新增正式 `action_selected` 为准，区分 engine/human/llm；未采用分支、UI 聚焦、候选节点、恢复前历史和开发选择不计入。主指标使用全体正式选择总数，来源表保留各来源数量及相同现实分母的比率。来源须在首次操作前确定且符合真实 player_actor；仅 `performance_controller` 标签随存档保留，恢复后的累计重新起算。

报告拆开正式计算、提前准备、认知、规划索引/展开/预测、Complete、投影、初始化和归档。操作的 inclusive/exclusive 总计与均值分列，只有排他值可沿嵌套求和；P95 是含子项时长的直方图上界。`trigger_actor`、`viewer`、`projection_scope` 分开，不能把触发全局投影的主体误当作该投影唯一成本所属者。`planning_work` 按正式事件累计实际候选检查、资格求值、预测、节点展开和峰值前沿，缓存命中的 `reused_work` 不重新累计，不修改预算、深度或顺序。

`StartPerformanceRecording` 从当前保留记录开始缓冲顺序写 `.dvlog.partial`；后续原始计算、正式选择和外部接口边界不因内存尾部淘汰而丢失。各类内存尾部最多 8192 条，供在线显示及新前缀快照导出。主录制只在正式关闭后写汇总与 seal 并原子发布，普通 `perf` 不关闭它，`perf NEW_PREFIX` 的原始文件明确为尾部快照。开启前缺失前缀、运行期写入错误和 partial 不能冒充完整证据。指标、原始边界、导出字段和片段归属详见 [引擎耗时记录](../../docs/engine-performance.md)。回放只消费已记录摘要，不创建新统计会话或把播放时长当作历史现实时间。

## 增量和缓存

每个 `DvSession` 持有独立的 `R.ValueContext` 与 `HostValues.Converter`。从初始化、恢复到正式运算、投影、交互记录和归档均在该上下文内使用 DV 值；嵌套作用域结束后恢复上一个上下文。快照池、可变父链、纯值备忘及原始记录器不由不同会话共享。转换器只跨调用缓存冻结的 DV 图，读取同一不可变节点可复用同一宿主只读值；转换器自己创建的只读值回写时可引用原冻结节点，外部可变容器只在单次转换中共享身份。

字段读取在核对主体访问后，以实际对象和字段为备忘依赖；其他对象、历史或全局时钟改变不会令该字段重复求值。静态对象名称、房间目录与区域使用叶项缓存，避免为了取得一个房间复制整个目录。主体地图按本人完整 mind、行动目录、已完成/已提供行动、实际 `pending_heard`、绑定、身份引用和当前人工选项复用几何；观察者称谓缓存也包含 `pending_heard`。实际游戏时刻、活动状态、章节、控制权每次覆盖。候选以完整主体、绑定、成本、控制类型和是否请求即时估值为依赖。普通投影只枚举合法候选并叠加真实已记录根分／选择分，缺失值不当作0；显式请求才重新计算即时估值，复用时保留真正的评分时刻。只读 DV 投影消费当前不可变状态，避免读取时实体化可变子树。客观数据仍不回填主体知识。

每个观察者独立缓存投影和最后交付版本。静态身份投影只在初次投影、地图或章节变化时构造；动态快照按会话版本构造，逐字段与上一份不可变 DV 值比较。未变字段复用相同的只读 .NET 对象，变化字段才转换。`HostStateDelta.Reset` 时宿主建立新快照，其他情况下合并 `Changed` 并移除 `Removed`。Godot 适配层将变化字段转成 Variant，再交接主线程。所有场景、节点、物理和 UI 操作留在主线程。

`Actors` 只取得轻量角色目录，不依赖全体观察者地图。主观预览按请求 viewer 构造和缓存；读取一个角色不主动展开其余角色的心智。客观地图仅在 includeObjective 或相应显式检阅请求中计算，同一 Version 的客观底图共享，观察者标记另行投影。界面重绘复用已取得的不可变结果；正式状态转换按实际版本和声明依赖失效，不跳过认知、调度或其他正式主体。缓存不是第二份可写世界状态。

Core 输出稳定的 actor、map、room、site、station 标识，`view_kind` 表示宿主视图用途，`location_title` 和 `game_time` 提供位置名称与逻辑时刻。Core 不携带 Godot 场景路径、sprite 名称、动画帧或 HUD 布局。Godot 在自己的 `Contracts/presentation_catalog.json` 和 `HostPresentation.gd` 中把这些身份绑定到场景、人物形象、站点标题与界面；CLI 直接消费中立投影。人物名称仍由观察者已经取得的认识决定。

投影中的 Rational 保持 `{"$dv_rational":"..."}` 标签。记录保持字段第一次出现的顺序和最后一个值；列表保持顺序，空列表保持数组。原生 DV 值没有暴露给宿主写入。

## 提前准备和后台调度

`PrepareAhead` 从当前状态的不可变快照建立独立分支。分支执行同一 DV 选择和 Prepare，使用原审计策略，暂存产生的事件。`R.UseRawRecording(false)` 阻止候选事件写入正式日志，候选输出也不进入控制台。token 绑定基版本和选择；采用时替换状态并发布实际新增事件，失效时交回正式 Prepare。Complete 继续核对行动事件、费用、权限、观察来源及权威前提。

后台入口只在人工等待且没有在途动作时执行。排队与实际动作仍由 DV 决定，Godot 收到 prepared 后完成真实现场执行。预备、后台和普通输入共用宿主串行工作者。

### 可中断等待与休眠

只有明确声明 `interruptible=true`、`activity="wait"`、`kind=wait` 或 `wait.*` 的等待进入 `dvs_interrupt_waits`；报价要求 `minimum.time=0`、`actual.time>0`，并禁止同时包含 answer、speech 或 travel。本人实际新听闻、观察及当前可见接触变化可提前结束等待；远处事件、其他主体私有变化不能唤醒本人。`Intent.activity_interruption` 保留原计划到期、实际时刻、`elapsed` 与来源，`execution_cost.time` 使用 elapsed。宿主仍提交匹配本次行动事件和实际时长的真实回执，经过 Prepare → receipt → Complete 结算，非时间费用不免除。

中断 Complete 不执行原完整等待法则，不将行动加入 `completed_actions`，也不留下普通拒绝签名阻止后续等待。回执以 `failure_class=activity-interrupted` 分类，和普通失败分别呈现；不可将中断解释为完成全部等待。固定点 guard 在完整认知捕获后可以容纳无实际来源的恒真 topic 条件，但不得据条件真值补造收闻或知识；待收闻、阻塞、本人输入或真实来源变化仍使休眠不成立。

## 退出保存点

会话打开、完成实际 Complete、或 Prepare 返回无 pending 的稳定边界时更新 `lastCommittedState`。动作已经准备而现场仍在执行时，退出存档取最近已提交快照；现场的半完成状态保留在轨迹中。恢复因此不会把 prepared 状态交给续读。完整轨迹与可恢复存档用途独立，`Trace` 不追加停止事件，显式 `Close` 只追加一次。

关闭前按最近请求的观察者取得实际 `closing_preview`，随后不推进世界；轨迹同时保留其 `closing_preview_state_version`。回放最终检查点据此显示真实结束地图；旧记录缺少它时不回填早期预览。

轨迹带结构化 `provenance`、`cut`、`lifecycle` 和 `pending_actions`，存档带 `archive_provenance` 与 `archive_cut`。来源按实际 host/executor、控制者、检阅者、是否 Godot 执行及可取得的源码/程序集信息填写；formal-model 不再由固定说明冒充 Godot 现场执行。

截止信息分别列 trace 与 durable 的游戏时刻、最后事件及状态版本。生命周期分别累计当前和存档点的选择、排队、Complete 与 pending；Complete 包含成功和拒绝的真实回执，结果由 outcomes 区分。`selections_not_in_save` 表示正式尾部选择尚未进入最近可恢复提交点，不能把它算成失败，也不能以 trace 选择数冒充存档选择数。排队尚未到期、等待宿主回执、关闭原因均按真实边界保留。

存档使用 `.dvsave`，轨迹使用 `.dvdag`，宿主记录使用 `.dvlog`。二进制写入先创建独占 partial 文件，完整封存后发布；恢复读取完整节点、序号、摘要及 seal。

普通事件的 `world_cut` 只在已有 live 原始 DAG 记录器提供真实引用时替代嵌套 `execution.world`。当前 CLI 没有打开 live `RawDagWriter`，最近实际会话未启用这条替代路径，不能据源码称压缩已经改善体积或耗时；`StartPerformanceRecording` 仅开启性能流。当前完整现实、认知及正式消费者依赖的历史仍保留，没有删除完整历史。

## 开发会话

Godot 用 `--dv-dev`，CLI 用 `--dev`。该配置写入 `story.development_mode` 并随存档保留。`inspect` 展示权威物理状态、指定主体的 mind、排队动作、最近规划结果和侵蚀字段。普通文本 `status` 只提供观察者的信息。

`tp` 与 `time` 调用 `dvg_dev_command`。位置命令通过开发法则提交，时间命令沿 due/环境边界处理已排队动作；开发时间结算明确记录 `development-formal-model`。结果位于 `development_result`，包括状态、说明及现场重同步标记。宿主随后同步物理表现。正式场景动作继续使用现场回执。

## 共享回放

`SmartReplay.Load(path, cancellationToken)` 读取 `.dvdag` 或 `.dvlog`，CLI 和 Godot 共用解析器。读取器核对二进制帧、DAG 引用链与封存信息，逐记录和节点响应取消；不可变共享节点只转换一次。完整会话轨迹按原始事件顺序读取，CLI 交互记录按发生时的事件游标插入导出轨迹，正式事件保持原值。

共享帧为 `ReplayFrame(sequence, kind, actor, action, text, duration, data)`，`data.contract` 为 `dv-replay-v2`，交互合同为 `dv-interaction-v1`。

| kind | 展示内容 |
| --- | --- |
| `focus` | 已记录的候选、评分、选中项及前态预览，持续 0.45 秒 |
| `confirm` | 同一交互的确认及实际结果；执行事件和呈现合并到此帧，后态只取已记录结果 |
| `cancel` | 清除已定位选项，保持未执行 |
| `snapshot` | 已记录检查点；无等待时间 |
| `input`、`presentation`、`visible`、`event` | 历史宿主输入、呈现及其他元数据，保留其来源 |

`interaction_id` 关联定位、确认和结果，`data.result_kind` 保留 move、dialogue、investigate 或 rejected，Godot 用它选择原演出路径。`record_index`、`input_record_index` 和 `result_record_indices` 指向原记录；展示帧避免重复嵌套完整预览。根快照可用 `snapshot_ref: "preview.snapshot"` 指定。原输入的时差保留在记录中，录制时的思考和静默不进入演示时钟。

旧轨迹存在实际 `action_execution` 时派生定位和确认两拍，`selection_recorded=false`；缺失的候选、评分和中间认知快照保持空。初始与最终检查点分别放在事件前后，不能拿最终状态补造中间现场。只有逻辑位置的旧记录仍只支持逻辑重建。

定位帧只读取已记录 option/action 的 `label`；执行正文、`surface` 和台词只进入确认后的结果。历史执行投影的 `label` 可能是结果正文，不能充当行动标签；标签缺失显示 `（未记录标签）`，不使用当前剧本或行动 ID 补写旧记录。检查点中的 `actors` 与 `viewer_title` 从同一检查点已记录的各主体本人认知提取，`name_source` 为 `recorded-actor-mind`，不替换任何主观地图成员的称呼。

旧交互的选项标签为空或等于行动 ID 时，读取器只从同份记录此前的匹配 `action_selected` 获取明确 `label`，取不到则显示 `（未记录标签）`；该适配覆盖交互与预览内的选项，不改写原始二进制。其他已记录标签保持原文；旧记录将观察正文写入标签的污染无法据文本可靠修复，需要用更新后的内容重新生成记录，才能取得明确的行动意图标签。

`Play(1/2/4/8)`、`Pause()`、`Step()`、`Advance(seconds)` 控制播放。每次更新最多交付一个新的可见拍，零时长元数据每次最多消费 256 帧。CLI 的交互播放持续读取 pause、step 和 quit；Godot 在独立回放场景中驱动 WorldView、StoryActor 和 UI。回放不向正式 DvSession 提交命令，也不产生新的观察。

## CLI 观察预览

`Preview` 的主观地图只读取指定角色 mind；客观地图读取 physical 与完整地点目录。房间包含区域信息与独立 `members` 列表，人物先于物体，各类内稳定排序；`is_current` 分别取对应状态中的本人位置。所有权、实际携带者与容器位置独立，物体只沿本投影已知且无冲突的位置链归组；携带物与容器内容附在对应主体或容器下。事件和谜题状态从空间成员中过滤，具有物理身份的开场道具仍是实物。主观不因客观名称、容纳关系或门状态存在就取得该信息。仅在双图检阅中，两边都已列出的同一成员具有明确而不同的位置或容纳关系时，附加认知差异标识；单边缺失不自动认定为认知错误。预览不刷新事实、不推进逻辑时刻、不改变会话版本。

预览结构为 `viewer / snapshot / status / subjective / options / actors / observers / performance`，显式请求时附 `objective`。`actors` 为轻量目录，`observers` 严格只包含本次请求的 viewer，`recorded_viewers=[viewer]`，`projection_scope` 为 `viewer-only` 或 `viewer-and-objective`；不把其他缓存视角混入本帧。目录读取不触发全角色地图。没有请求的视角不补造成已经取得的历史投影；回放的某一视角缺失时保持缺失。选项和评分按实际请求取得，缓存按会话版本失效，重绘复用不可变结果。CLI stdout 按当前观察者与地图模式裁剪；记录保留本次实际请求的预览，不为封存主动展开全体角色。

`performance` 独立于物理世界和人物认识。摘要另带实际 `lifecycle` 与按主体区分的 `decisions`，以便把累计选择、实际完成（含拒绝）和在途数与每小时速率分开；自主摘要包含会话 lifecycle 及全角色选择行，人工/LLM 摘要的新增角色行仅包含受控人物且不附全局 lifecycle。`decision_actor_scope` 标明新增角色行覆盖 `all` 还是单个主体，只有该范围内缺少行才能认作真实零选择；旧记录缺失范围不补零。完整 `PerformanceSnapshot` 仍是显式工程统计，包含全角色细分。在本帧所需投影完成后取一次新摘要，交互帧顶层与嵌套预览使用同一份统计；缓存预览不缓存时钟。`measured_until_ticks` 和 Stopwatch 频率标明采样截止，状态版本标明世界截止，二者分开；旧回放缺失时不补造。

`snapshot.actors` 和各 `observers[id].snapshot.actors` 同步保存预览的行政人物目录，供回放视角栏显示姓名；主观成员仍独立采用该观察者实际取得的身份信息。已经 prepared 而不在当前候选集中的动作保留原行动 `label`、实际费用及选择事件来源；标签先取匹配的 `action_selected`，再取当前意图/原行动，均缺失时显示 `（未记录标签）`。

### 主观称谓、行动标签与结果回执

`dvg_pending_self_names` 只从本人已实际收到、带来源的 `pending_heard` 中读取说话人对自身姓名的明确字面陈述。尚未完成认知处理时可以只读显示“自称某某”；这不是已采纳知识，不写回 mind，也不授权候选、行动或判断。本人已经取得的姓名优先；仍未知的人物使用稳定的“陌生学员·编号”，未辨明的铜口发言使用“铜口里的声音”。匿名编号只供界面区分，不泄露作者姓名。

候选的 `label_parts` 仅从行动者自己的无争议姓名字段解析；字段缺失时使用匿名称谓，不从客观目录或待处理自介补填。正文中的姓名和回放称谓按实际观察者投影，标签继续只表达行动意图。

`Presentation` 的 `judgment` 只携带本人真实 `answer_accepted/answer_rejected` 事件的状态、事件标识、游戏时刻、实际 `reason` 与 `stage`。内容层只按真实结果附加 `formal-receipt` 文本，不能将系统判答编成恶魔对白，也不序列化未公开的私有前提。本人普通失败给出保守执行反馈，`activity-interrupted` 给出等待被新动静打断的独立反馈，不显示未实际取得的结果正文。

CLI 当前源码将最新正文全宽置于候选和地图之前；引擎默认只显示选中项的已有短分，`scores on` 与 `zoom detail` 展开相应细节，人工和 LLM 默认隐藏评分。录制停止不等于剧情完成。JPG 按真实字体 Unicode cmap 选择可用字形，缺字时可用 `--fallback-font`，保留原始 `✕`、`✓`、SVG 格宽与基线；不能替换符号或用缺字方框冒充证据。

选项区分当前即时估值、历史实际根评估及选择时的路径分。`ai_score` 与 `score_kind` 一起消费，来源保留事件、认知修订和游戏时刻；已经排队的动作可以保留选择时的路径分，同时显示当前即时分，两者不能被解释为同一时点同一口径。历史选择路径来自真实 planning_choice/action_selected 记录，不用当前瞬时值倒推。

| 口径 | 分数与分项 | 来源 |
| --- | --- | --- |
| 当前即时 | `immediate_score`、`immediate_breakdown` | `score_game_time`、`mind_revision` |
| 已记录根评估 | `root_score`；显示时 `ai_score`、`score_kind=recorded-root` | `root_event`、`root_game_time`、`root_mind_revision`，以及 `score_event` |
| 选择时路径分 | `selection_score`、`selection_score_kind=recorded-path`、`selection_path` | `selection_event`、`selection_game_time`、`selection_mind_revision` |

根分的 `score_breakdown` 只包含 `scope=recorded-root`、`total`、`source_event` 和 `components_recorded=false`，明确没有记录可供展开的历史组成，不能把即时组成复制进去。在途候选保留 `action_state=queued` 或 `prepared`、`selected=true`，用于展示原选择的状态和理由。

`planning_status=evaluated-root` 表示确有对应根评估；实际规划记录缺少该根条目时标记 `unexpanded`，独立即时估值仍可存在。仅凭负分、同分或即时值大小不能推断是否展开。`immediate_breakdown` 只对应当前即时估值，不能给历史根分或路径分套用该分项。


### 物体、地点与通道字段

内容对象以 `map_kind`、`map_title` 提供中立展示类别与名称，实际物理位置写入 `state.location`。成员输出 `id/title/kind/is_viewer`，以及独立的 `owner/owner_title/carrier/container`；后两项引用同视图的成员身份，CLI 只将它们转换成“携带／内含”的层级。房间输出 `region` 和 `logical_location`，标题采用“区域 · 房间”。通道输出 `state/state_label/known/state_refs/state_scope`，物理开闭与管理通行资格保留不同来源，没有状态证据时保持未知。`passage_observed` 单独记录过去真实通行的事实和来源，不把历史通过改写成当前永久开放。

普通环境感知由声明的 `visible_fields` 仅发布直接同房对象的中性 name/location。该观察发生在正式认知/结算边界，不在预览读取时授予事实。只知道同房名称与位置不等于取得检视内容、谜题答案、随身物品或容器内部；候选标签、客观地图和角色目录都不能替代主体实际观察。

风衣初始位于 R-05，放大镜、石墨笔与证物袋在风衣内。穿衣成功后风衣的位置才变为侦探，内含物仍属于风衣；衣袋名称与容纳关系由实际 E12 检视完成后发布。所有权不能替代实际携带位置，显示元数据也不授予隐藏物品知识。开场的领取柜、银元、金属试件、试听机与茶具具有正式物理身份，保留为实物；抽象记录不进入实物列表。

### 即时评分分项

选项的 `immediate_breakdown` 对应当前即时估值，保留 `scope` 和 `formula`。它包含信息、验证、进展、成就四项原值及加权值，信息权重、进展权重、机会价值、时间罚项、总值、机会清单及原始 `value_details`。当前公式为：

```text
action_value = (information + verification) * information_weight
             + (progress + achievement) * progress_weight
total = action_value + opportunity * progress_weight - actual.time * time_cost
```

`computation_penalty` 与 `stamina_penalty` 为 0，明确当前即时公式没有这两项扣分；实际行动费用仍独立保留在 `cost`。分项直接读取现有估值函数，不改变权重、预算、费用或行动顺序，也不为了区分候选人为打破同分。若展示分数来自已记录根节点或最终选择，仍按 `score_kind/score_event` 标识；当前即时分项不能冒充历史规划分数的拆解。

### 正式转移的不可变输入

Host 将当前状态快照传入正式 Prepare、后台 Prepare、Complete、Presentation、Close 与只读投影。DV 在实际写入点取得独立事件列表，通过持久字段／主体替换构造新状态；不会在借来的冻结值上追加日志。后台无变化以输入的冻结状态引用比较，避免可变包装与冻结输入的身份不同而误增版本。提前准备也使用独立分支及冻结读取，采用和原始事件发布仍遵从原边界。

本批等待中断、休眠 guard、称谓与结果呈现、CLI 布局及字体回退尚只完成源码修改，下一轮正式会话、Godot 现场与逐图表现均未验收。规划器内部墙钟硬期限仍未实现；外部限时采集器不能证明单次规划已具备硬期限。
