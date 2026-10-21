# DV 会话与双宿主接口

`DvSession` 持有唯一权威会话，Godot 和 `dv-cli` 共用同一 Prepare → Execute → Complete。生产内容由 `../modules/60-game.dv` 组合，基础机制由 `../base/DuelView.Core.csproj` 提供；Godot 依赖停留在游戏适配层。轨迹工具及 oracle 位于 `../checks`，没有进入生产程序集。

## 接口

| 接口 | 用途 |
| --- | --- |
| `OpenConfiguredCampaign(options)` | 接受 `controller`、`viewer`、`campaign`、`audit`、`developer` 开局配置 |
| `PrepareValue(selection)`、`Intent` | 通过 DV 重核候选，返回当前意图的只读 .NET 值图 |
| `CompleteValue(receipt, viewer)` | 接收现场回执、结算权威效果、返回观察者呈现与真实 `outcome` |
| `Project(viewer, forceFull)` | 返回 `HostStateDelta`，含版本、基版本、重置标志、变化字段和删除字段 |
| `Materialize(viewer)` | 返回同版本缓存的完整只读投影，供初次安装及文本兼容接口读取 |
| `ExecutorProjection(actor)` | 执行者与观察者相同时复用；其他执行者只投影现场所需的数据 |
| `PrepareAhead(selection)`、`TryAdoptPrepared(token, out intent)` | 提前计算候选分支；正式选择在原状态版本仍有效时采用 |
| `CancelPrepared()` | 丢弃候选分支，保持正式状态与费用 |
| `PrepareBackgroundValue()` | 在人工等待边界调度同刻 NPC，沿唯一 pending/Complete 闭环执行 |
| `TextStatus()` | 当前观察者的位置、同房人物、近期实际观察及输入状态 |
| `Inspect(actor)`、`ExecuteDebug(command, argument)` | 显式开发会话中的双状态查看、位置及时间命令 |
| `Close()` | 追加一次关闭事件并缓存已封存轨迹，重复调用保持结果 |
| `Trace()`、`WriteTrace(path)` | 读取当前或已关闭的轨迹，不关闭会话 |
| `Save()`、`WriteSave(path)` | 保存最近已提交的稳定状态 |
| `Restore(value)`、`RestoreFile(path)` | 恢复稳定会话，保留审计和开发模式 |

`Prepare`、`Complete`、`Snapshot` 继续提供进程内 JSON 交换。正式 Godot 路径使用值图与增量接口，省去 JSON 编码和重新解析。文本协议的 JSONL 用于 stdin/stdout；持久轨迹保持二进制格式。

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

`SmartReplay.Load(path)` 读取 `.dvdag` 或 `.dvlog`，CLI 和游戏控制台共享一个解析器。读取器核对原始二进制帧及 DAG 引用链，依记录顺序提取检查点、行动、对白、原始设备输入、现场快照、可见正文和开发事件。原始日志不被修改，权威事件与玩家呈现保留各自来源。

共享帧为 `ReplayFrame(sequence, kind, actor, action, text, duration, data)`，`data.contract` 为 `dv-replay-v2`。`data.record_index` 保留记录序号；已有的 `seq`、`frame`、`state_version`、`action_event`、`presentation_id`、`interaction_id`、`cause_input`、`viewport_size`、`real_us`、`physics_frame`、`bodies`、`context` 和 `casebook` 原样透传。宿主据这些关联恢复已记录的身体轨迹与 UI 页面。

| kind | data 中的内容 |
| --- | --- |
| `snapshot` | 完整中立 `snapshot`；新输入日志的 `replay_snapshot` 同时携带真实身体和 UI 上下文 |
| `input` | 完整原始 `input`、设备和已有回执；`recorded_interval_seconds` 保留相邻输入的原现实时间差 |
| `presentation` | 已交付玩家的完整 `presentation` 与当时文本 |
| `move`、`dialogue`、`investigate`、`rejected` | 已记录的行动及结果；移动包含逻辑 `position`，实际完成回执存在时附 `receipt` 和 `actual_position` |
| `visible` | 完整 `visible` 记录及 `texts`、`panel` |
| `event` | 其他已记录事件的身份、正文和来源；完成回执也保留为独立事件 |

DAG 初始检查点先于事件播放，最终检查点在已记录事件之后发布。完整会话使用只读快照投影；旧轨迹仅有 `physical/actors` 时使用 `dvg_recorded_snapshot`，只读出已记录的逻辑现场与人物，未记录的案卷和地图知识保持空。旧日志没有实际身体坐标时，宿主只能呈现逻辑位置重建，不能宣称复现原操作轨迹。

节拍由内容决定：移动和带身体记录的输入 0.3 秒，调查 0.6 秒，标题与对白按文本长度限制在 0.8–2 秒；元数据与检查点不加入等待。输入保留的原时差供身体路径解释，录制时的静默时间不加入播放时钟。`Play(1/2/4/8)`、`Pause()`、`Step()`、`Advance(seconds)` 控制播放。Godot 在独立回放场景中使用同一帧驱动 WorldView、StoryActor 和 UI；正式 DvSession 不接收回放命令，回放也不生成新的观察。
