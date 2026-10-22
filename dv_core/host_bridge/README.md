# DV 会话与双宿主接口

`DvSession` 持有唯一权威会话，Godot 和 `dv-cli` 共用同一 Prepare → Execute → Complete。生产内容由 `../modules/60-game.dv` 组合，基础机制由 `../base/DuelView.Core.csproj` 提供；Godot 依赖停留在游戏适配层。轨迹工具及 oracle 位于 `../checks`，没有进入生产程序集。

## 接口

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
| `TextStatus(viewer)` | 指定观察者的位置、同房人物、近期实际观察及输入状态；无参数时使用会话观察者 |
| `Actors`、`ObserverOptions(viewer)` | 预览角色清单、指定角色当前可选行动及实际认知评分 |
| `Preview(viewer, includeObjective)` | 按角色读取主观地图；显式请求时附完整客观地图，保持会话版本与认知不变 |
| `IsScheduled(actor, actionId)` | 只读核对该人物的选择是否仍在调度队列，不推进状态 |
| `RecordInteraction(payload)` | 记录 focus、confirm、result、cancel，与正式事件保持因果次序 |
| `Inspect(actor)`、`ExecuteDebug(command, argument)` | 显式开发会话中的双状态查看、位置及时间命令 |
| `Close()` | 追加一次关闭事件并缓存已封存轨迹，重复调用保持结果 |
| `Trace()`、`WriteTrace(path)` | 读取当前或已关闭的轨迹，不关闭会话 |
| `Save()`、`WriteSave(path)` | 保存最近已提交的稳定状态 |
| `Restore(value)`、`RestoreFile(path)` | 恢复稳定会话，保留审计和开发模式 |
| `PerformanceSummary()`、`PerformanceSnapshot()` | 只读轻量摘要及完整分项/小时汇总，不推进会话 |
| `WritePerformanceReport(prefix)` | 导出中文报告、小时/操作聚合 CSV 及二进制采样 |

`Prepare`、`Complete`、`Snapshot` 继续提供进程内 JSON 交换。正式 Godot 路径使用值图与增量接口，省去 JSON 编码和重新解析。文本协议的 JSONL 用于 stdin/stdout；持久轨迹保持二进制格式。

## 引擎统计

所有正式会话常开低频作用域计时，按实际前进的游戏分钟折算每游戏小时的现实计算秒数。正式计算、提前准备、认知、选策、Complete、投影、初始化和归档分别记录，嵌套只以排他耗时求和。原始尾部采样最多 8192 条，累计与小时汇总保持完整；零推进保留缺失比率。指标、异常范围、导出格式和小时归属详见 [引擎耗时记录](../../docs/engine-performance.md)。只读回放仅消费已记录摘要，不创建新会话或把播放耗时当作历史引擎耗时。

## 增量和缓存

每个观察者独立缓存投影和最后交付版本。静态身份投影只在初次投影、地图或章节变化时构造；动态快照按会话版本构造，逐字段与上一份不可变 DV 值比较。未变字段复用相同的只读 .NET 对象，变化字段才转换。`HostStateDelta.Reset` 时宿主建立新快照，其他情况下合并 `Changed` 并移除 `Removed`。Godot 适配层将变化字段转成 Variant，再交接主线程。所有场景、节点、物理和 UI 操作留在主线程。

Core 输出稳定的 actor、map、room、site、station 标识，`view_kind` 表示宿主视图用途，`location_title` 和 `game_time` 提供位置名称与逻辑时刻。Core 不携带 Godot 场景路径、sprite 名称、动画帧或 HUD 布局。Godot 在自己的 `Contracts/presentation_catalog.json` 和 `HostPresentation.gd` 中把这些身份绑定到场景、人物形象、站点标题与界面；CLI 直接消费中立投影。人物名称仍由观察者已经取得的认识决定。

投影中的 Rational 保持 `{"$dv_rational":"..."}` 标签。记录保持字段第一次出现的顺序和最后一个值；列表保持顺序，空列表保持数组。原生 DV 值没有暴露给宿主写入。

## 提前准备和后台调度

`PrepareAhead` 从当前状态的不可变快照建立独立分支。分支执行同一 DV 选择和 Prepare，使用原审计策略，暂存产生的事件。`R.UseRawRecording(false)` 阻止候选事件写入正式日志，候选输出也不进入控制台。token 绑定基版本和选择；采用时替换状态并发布实际新增事件，失效时交回正式 Prepare。Complete 继续核对行动事件、费用、权限、观察来源及权威前提。

后台入口只在人工等待且没有在途动作时执行。排队与实际动作仍由 DV 决定，Godot 收到 prepared 后完成真实现场执行。预备、后台和普通输入共用宿主串行工作者。

## 退出保存点

会话打开、完成实际 Complete、或 Prepare 返回无 pending 的稳定边界时更新 `lastCommittedState`。动作已经准备而现场仍在执行时，退出存档取最近已提交快照；现场的半完成状态保留在轨迹中。恢复因此不会把 prepared 状态交给续读。完整轨迹与可恢复存档用途独立，`Trace` 不追加停止事件，显式 `Close` 只追加一次。

存档使用 `.dvsave`，轨迹使用 `.dvdag`，宿主记录使用 `.dvlog`。二进制写入先创建独占 partial 文件，完整封存后发布；恢复读取完整节点、序号、摘要及 seal。

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

预览结构为 `viewer / snapshot / status / subjective / options / actors / observers / performance`，显式请求时附 `objective`。`performance` 为会话诊断摘要，独立于物理世界与人物认识；新交互记录随预览保留，旧回放缺失时不补造。`observers[id]` 保存该角色的认知地图和展示快照；未计算的选项评分保持空。缓存按会话版本失效，界面重绘复用结果。CLI 的 stdout 按当前观察者与地图模式裁剪；完整预览仍保留在二进制回放记录中。

`snapshot.actors` 和各 `observers[id].snapshot.actors` 同步保存预览的行政人物目录，供回放视角栏显示姓名；主观成员仍独立采用该观察者实际取得的身份信息。已经 prepared 而不在当前候选集中的动作保留原行动 `label`、实际费用及选择事件来源；标签先取匹配的 `action_selected`，再取当前意图/原行动，均缺失时显示 `（未记录标签）`。

选项 `ai_score` 必须与 `score_kind` 一起消费：`immediate` 是当前只读单步估值，`recorded-root` 来自同认知修订的实际单步规划条目，`recorded-choice` 来自最终选择事件。`score_event` 指向该条记录。`planning_status=evaluated-root` 表示已有实际根节点评估；有同修订规划记录而缺该候选根条目时标记 `unexpanded`，同时保留其独立的即时估值和 `planning_event` 来源。仅凭数值 `-0.01` 不能推断未展开；有根评估记录的负分照实显示。


### 物体、地点与通道字段

内容对象以 `map_kind`、`map_title` 提供中立展示类别与名称，实际物理位置写入 `state.location`。成员输出 `id/title/kind/is_viewer`，以及独立的 `owner/owner_title/carrier/container`；后两项引用同视图的成员身份，CLI 只将它们转换成“携带／内含”的层级。房间输出 `region` 和 `logical_location`，标题采用“区域 · 房间”。通道输出 `state/state_label/known/state_refs/state_scope`，物理开闭与管理通行资格保留不同来源，没有状态证据时保持未知。

风衣初始位于 R-05，放大镜、石墨笔与证物袋在风衣内。穿衣成功后风衣的位置才变为侦探，内含物仍属于风衣；衣袋名称与容纳关系由实际 E12 检视完成后发布。所有权不能替代实际携带位置，显示元数据也不授予隐藏物品知识。开场的领取柜、银元、金属试件、试听机与茶具具有正式物理身份，保留为实物；抽象记录不进入实物列表。

### 即时评分分项

选项的 `score_breakdown` 对应当前即时估值，保留 `scope` 和 `formula`。它包含信息、验证、进展、成就四项原值及加权值，信息权重、进展权重、机会价值、时间罚项、总值、机会清单及原始 `value_details`。当前公式为：

```text
action_value = (information + verification) * information_weight
             + (progress + achievement) * progress_weight
total = action_value + opportunity * progress_weight - actual.time * time_cost
```

`computation_penalty` 与 `stamina_penalty` 为 0，明确当前即时公式没有这两项扣分；实际行动费用仍独立保留在 `cost`。分项直接读取现有估值函数，不改变权重、预算、费用或行动顺序，也不为了区分候选人为打破同分。若展示分数来自已记录根节点或最终选择，仍按 `score_kind/score_event` 标识；当前即时分项不能冒充历史规划分数的拆解。
