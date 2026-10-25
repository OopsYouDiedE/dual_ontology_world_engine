# DV 生产模块与宿主接口

`20-core.dv` 提供通用值、世界、法则、观察与有限域求解。`30-session.dv` 包含人物认知、预算、排队、调度与 Prepare/Complete。`35-campaign-schema.dv` 统一战役人物、位置、时钟、通行权、构造器、目标时间窗口与跨章承接字段。`40-chapter1.dv` 和 `50-chapter2.dv` 各自 include 35。`36-island-topology.dv` 是真实几何合同的生成数据；`55-island.dv` include 36和50，声明数值输运、日账、苗床及正式交互。`56-island-cases.dv` include 55，以已结日账匹配持续案件角色；候选、实际痕迹、观察和核账仍走原正式动作链。`60-game.dv` include 56并组合连续章节及宿主投影，打开第一章即安装共享数值对象，扩章保留状态并安装第二章交互。独立章节编译读取各自 include 闭包。

战役共享函数使用 `campaign_` 前缀；既有通用配置构造器 `cognitive_erosion` 也由 35 提供，保留曝光引用、阈值、耦合系数和依据字段。第一章的早晨话语锚和答辩主题封装继续留在 40。第二章使用当前发言时间锚与通用行动封装。`case.*`、`night.first`、契约正文和已知人物姓名的读取继续使用原持久身份；schema 声明不授予观察。第二章须在有相应承接对象的场景上扩展，其动作仍由本人前提与权威前提共同决定。

## 生产投影

| DV 入口 | 返回值与用途 |
| --- | --- |
| `dvg_snapshot_static(state, viewer)` | 中立视图类别与玩家 actor 身份；同观察者同图复用 |
| `dvg_snapshot_dynamic(state, viewer, intent)` | 时刻、位置、准入、人物身体、当前意图、本人地图知识、观察者案卷重演视图与章节呈现 |
| `dvg_executor_snapshot(state, actor, intent)` | 现场执行使用的地图、房间、身体、准入和意图，不计算第二份观察者重演 |
| `dvg_snapshot_with_intent(state, viewer, intent)` | 静态与动态合并的完整首屏接口 |
| `dvg_recorded_snapshot(record, viewer)` | 从旧轨迹已有的 physical/actors 投影只读回放现场，不创建会话或补充认识 |
| `dvg_prepare_background(state, viewer)` | 玩家等待期间处理当前时刻的一个 NPC 决策或已到期意图，未来行动保留队列 |
| `dvg_trace_snapshot(state)` | 读取当前轨迹，不关闭会话或追加事件 |
| `dvg_dev_command(state, command, argument)` | 显式开发会话的 `tp` 与 `time`；结果位于 `story.development_result` |

具体物理坐标位于 Godot 的 `Contracts/geometry_contract.json`，场景、人物形象、站点标题和 sprite/site 映射由 `Contracts/presentation_catalog.json` 与 `HostPresentation.gd` 绑定。Core 传稳定的逻辑地点、执行房间、站点和 actor ID，并提供 `location_title` 与逻辑 `game_time`。人物投影保持身份、位置和观察者已知名称，场景资源、sprite 和动画帧由宿主补充。增量版本、缓存、投机分支与接收后的状态图合并由 host_bridge 负责。投机分支必须隔离原始记录；采用后只发布被采用分支的新增事件。真实 Complete 继续核对当前权威前提。

`map_knowledge` 返回 `rooms: [{map, room, label}]` 与 `doors: [{from: {map, room, label}, to: {map, room, label}}]`。房间来自本人的已知位置与定位观察历史；门路来自本人已完成移动和曾实际获得的合法行动。`offered_actions` 在玩家输入阶段记录已准入 ID，让看过但尚未走过的门路随存档保留。投影可从存档的认知、完成动作和已准入 ID 恢复，不读取几何源中的全部房间或授予远处人物位置。人物投影中的可选 `anchor` 是当前逻辑位置的站点 ID；开发命令同步身体时使用该值，常规初次放置继续使用 `initial_anchor`。

## 行动标签与结果正文

`campaign_intent_action(..., label, source)` 声明行动意图；`campaign_surface_action(..., label, text, source)` 分开接收意图与完成正文。40/50 的调查、对话、提案和活动记录分别保存 `label` 与 `surface`/`text`/`speech.surface`，标签不复制检视结果或整段台词，询问物件的标签保留具体物件名。行动资格、费用与权威前提保持各自原有来源。

`dvs_action_label(actor, action)` 是玩家选项、观察者选项和实际选中行动共同使用的标签入口，只读取 `label`；缺失时为 `（未记录标签）`。可选 `label_parts` 由文本片段或 `{ref, fallback}` 片段组成，引用仅从本人 `mind` 读取，未知人物使用“同伴”。选中时把当时解析出的标签写入 action 并随 `action_selected` 保存。结果正文与台词仍由成功的 Complete 产生，选项不得回退读取结果正文。

主观姓名读取本人已持有的 `identity.name`、`state.name`、`opening.personal.<id>.state.name` 或 `c2.dialogue.person.<id>.state.name`；新寝区与第二章自报姓名通过原有话语解析传播。自身保留姓名，其他人物未被认识时保持“同伴”。旧轨迹若没有实际姓名字段，保留“本人”或“同伴”，不按行动 ID 或现行人物目录补写历史姓名，不反写世界或补造观察。

## 规划与惰性认知

`plan_depth` 和 `lookahead_limit` 分别控制前瞻层数与有序 beam 宽度，原 planning_limit 控制工作预算。beam 按分数降序、耗时升序排列，同分同耗时保留声明次序。visited 只包含实际展开节点；被 beam 淘汰的候选不占用支配记录。默认展开已声明层数；`greedy_root_stop` 显式选择即时收益根停止，`positive_move_root_stop` 显式允许正收益移动根停止，`fast_approximation` 保留既有一层近似语义。

认知固定点保存完整本人语义输入与本人事件游标。空闲休眠比较本人状态、收到的事件、房间可见人物、绑定、费用、内容及下一个声明时间窗，不因远处无关人物行动反复唤醒。待理解话语、本人修订、现场人物变化、未完成的认知与时间边界重新唤醒。话语预测仅为当前可见听众或已声明的远距声道计算；实际说话与收听仍在 Complete 通过原正式声道结算。

## 开发命令与诊断隔离

`tp` 通过标记为 development 的临时权威法则改变位置，只向本人交付位置观察，宿主据开发结果同步身体。`time +N` 按环境时间窗和排队 due 逐边界推进，到期动作使用同一 Prepare/Complete 与 `development-formal-model` 回执；它保留原配额和时域，达到预算时返回实际停点。开发命令写入同一会话事件流，普通会话不开放这些命令。

`../checks/65-debug-runner.dv` 单向 include 生产 60，包含 formal-model 循环和阶段恢复。`../checks/70-checks.dv` include 65，保持 oracle 与生产闭包分离。`domain_evaluation_complete` 仅在领域分析已经返回完整报告时为真，负面领域裁决也属于完成；未进入时以 `domain_evaluation_reason` 给出完整性、初始状态、轨迹或配置原因。`main_causal_supported` 独立报告具体因果结论。`dvs_session_close` 追加一次停止事件，重复读取使用只读轨迹接口。

源码实施状态与实际运行结果分别报告。构建、检验和行为测量按用户本轮授权安排。
