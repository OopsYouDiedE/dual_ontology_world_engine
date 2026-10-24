# Godot 宿主维护

## 职责与工作顺序

本目录负责场景、渲染、物理、输入、界面、音频和应用生命周期。`DualOntologyWorldEngine.csproj` 单向引用 `../dv_core/host_bridge/DuelView.Session.csproj`。Core 负责世界权威、主体认知、规划、报价、行动效果和观察；宿主执行正式意图并呈现收到的数据。

用户当前指令决定实现范围、测试时机和运行时限；禁止测试、检验、构建或探针时，只阅读实现所需源码并修改。要求集中修改时，全部源码、公共接线和文档完成后才进入获准的运行窗口，不从历史命令或工具默认时限推定额外授权。采用明确的数据所有权与状态转换，不加入多层回退或第二套动作规则。多人协作按文件划定所有权，共享检出不由子代理自行提交。源码和文档完成不表示运行已经通过。

## 模块

| 路径 | 职责 |
| --- | --- |
| `scenes/Host/Main.gd` | 组装宿主、呈现队列、会话事件路由、书签交接 |
| `scenes/Host/HostFlow.gd` | Main 的三个状态轴，由集中 transition 管理状态转换 |
| `scenes/Host/GameEngine.cs` | 唯一正式 DvSession、串行后台命令、只读快照缓存、开发和回放桥 |
| `scenes/Host/GodotHostValues.cs` | Core 原生只读值图到 Godot Dictionary/Array 的转换 |
| `scenes/Host/CasebookPersistence.cs` | 世界书签与观察者案卷的配对保存、恢复 |
| `scenes/Host/DvFieldExecutor.gd` | 保留人物身体、离屏物理世界、移动事务与占位 |
| `scenes/Host/HostPresentation.gd`、`Contracts/presentation_catalog.json` | 将 Core 的中立语义投影绑定到 Godot 呈现资源 |
| `scenes/Host/RuntimeDiagnostics.gd`、`PlayerViewTrace.gd` | 常开输入事件记录及显式启用的帧诊断、可见记录 |
| `scenes/Host/DevConsole.gd` | CanvasLayer 128；控制、双本体与规划、性能、回放四面板 |
| `scenes/Host/HostLifecycle.gd` | 统一退出顺序：宿主完成关闭、音频释放、SceneTree 退出 |
| `scenes/Host/GeometryCatalog.gd` | 几何合同的唯一运行读取、路由、锚点及场景绑定入口 |
| `scenes/Host/MockSession.gd` | 纯 GDScript 演出样本，读取 `Contracts/presentation_mock_pack.json` |
| `scenes/Host/ReplayStage.gd`、`ReplaySession.gd` | 独立 SubViewport 中的真实场景、人物、碰撞和书页回放 |
| `scenes/UI/ReplayPreferences.gd` | 回放界面的独立内存设置与只读入口约束 |
| `scenes/Shared/Field/WorldView.gd` | 人物、连续行走、空间输入、视野与房间舞台 |
| `scenes/Chapter2/Field/CampusMapView.gd` | 地图装配、导航、碰撞、门与跨图执行 |
| `scenes/UI/UISystem.gd` | 子界面总装、单一模态所有者、纯数据事件 |
| `scenes/UI/TitleUI`、`CasebookUI`、`BookSettingsPage` | 标题、案卷阅读、设置书页 |
| `scenes/UI/DialogueUI`、`InvestigationUI`、`CampusMapPanel` | 对白、调查选项、已知地图 |

详细事件合同见 `scenes/Host/README.md`。Core 输出 `view_kind`、`actor`、`location_title` 等中立语义字段；Godot 的 res 资源由 HostPresentation 与 presentation_catalog 增补。Core 不依赖 Godot，UI 不持有 Core 对象。Main 只在正式会话路径动态加载 C# 入口；纯 GDScript Mock 可独立启动。

## 权威与执行

近似模拟的 `simulation_pulse` 在宿主空闲已提交边界每现实秒至多入队一次；GameEngine 沿同一后台队列调用 `DvSession.SimulationPulse`。回放与 Mock 不发送该命令，正在执行的动作等待完成。脉冲只刷新近场范围和现有事实知情映射，实际游戏时间及权威行动沿正式链推进。恢复后的现实秒由会话实例重定基点。

`--highlights` 或 `--highlight=<id>` 在创建正式会话前打开 `scenes/Highlights/Highlights.tscn`。六幕舞台读取 `Contracts/highlight_stages.json`，范围为在场演出，实际操作不写入正式世界存档。自动地形、物件与人物沿各自源管线重建。

正式链为 `Prepare → Godot 现场执行 / 世界法则回执 → Complete → 呈现`。`execution_snapshot` 只服务执行主体；玩家文字与案卷来自观察者投影。执行者等于观察者时复用同一快照。GameEngine 通过 `Project` 接收 StateDelta，缓存静态字段，转换变化字段；正常动作路径不往返 JSON。文件持久化与人可读状态导出使用各自的序列化边界。

GameEngine 在单一后台队列中使用 DvSession，工作线程不访问场景节点。主线程接收已完成的数据图后只读使用。UI 编辑另建自己的数据。正式动作 pending 期间不得重新分派该动作；重复 Complete 按 action_event 去重。开发命令输入错误只返回控制台，普通动作中无法确定提交状态的异常由会话错误入口处理。

`view_ready`、`dv_next` 调用正式 Prepare。`npc_tick` 仅在玩家等待输入、宿主无在途动作时调用 `PrepareBackgroundValue`；Core 选择同一时刻的 NPC 工作，不越过玩家等待时刻。现场局部行走继续进行。阅读中的 NPC 可通过同一执行器移动；Complete 后同步身体，观察者新对白排队。读取旧呈现队列时，低版本快照不得覆盖现场已接纳的新版本。

门前 48 px 与点击路线触发 `prepare_ahead`。Core 在分支计算 Prepare，返回包含状态版本与 choice 的令牌；此阶段不发布正式行动或花费。实际选择携带令牌，Core 接纳有效结果；版本改变或意图取消即作废。玩家选择仍由正式空间入口核对当前选项与接触。

## 移动事务

所有 move、station 与跨图移动，包括玩家和 NPC，开始执行时保存 rollback anchor：原地图、房间、连续脚点、源投影与动作标识，并保留实际走过的 trail。原位置作为占位保留到 Core Complete 返回真实 outcome。`applied` 提交并释放占位；`blocked` / `rejected` 时，玩家沿真实 trail 反向行走并接受碰撞，恢复原地图与障碍后释放事务；离屏 NPC 可直接恢复到其已保存锚点。离屏与可见世界共享身体登记，跨图交接在结算后接入可见场。

每次实际移动事务将归一化轨迹写入原回执的 `receipt.field.trajectory`。跨图记录保留 `entered_by` 的真实 from/to 映射，供同一事件的回放消费；轨迹记录不产生额外 Complete 或行动费用。

`restore_movement_anchor` 返回恢复是否完成。只有成功恢复才释放回滚锚点与占位；地图准备失败或场地已关闭返回失败，保留事务，并由 `GameEngine.RecordHostFailure` 登记具体失败供关闭封存使用。宿主不得把未完成恢复报告为成功，也不重新提交已经完成的 Core 动作。

门槛身份按人物连续脚点解释，导航格中心不能替代脚点。正常动作到达依靠真实碰撞、路径和门口移动，不在末尾将人物写到目标位置。首次物化使用有效初始锚点或房内合法空位。`--dv-dev` 的显式 tp/time 可重建全部身体，并按权威 map / room / anchor 重新绑定；这是开发编辑入口。

宿主不推进游戏钟，不构造知识、剧情资格、报价或效果。完整费用来自 execution_cost；是否应用由 Complete 决定。纯 UI 阅读、地图、鼠标提示和案卷编辑不改变世界。

## 退出与事务排空

Main 进入关闭时停止新输入与新动作请求，继续接收已经进入执行的动作回执、发送 `dv_complete` 并处理 `dv_commit`。尚未进入 `_dv_execute` 的 prepared 动作保留 GameEngine 的 `pendingEvent`，由原始轨迹和关闭清单明确记录未执行，存档使用最近提交点；关闭不为这些动作生成 Complete，避免凭关闭操作收取行动费用或扣减预算。手动过门的在途动作停止输入后产生一次 blocked 回执，随后按 Core 返回的真实结果统一提交或回滚。

关闭等待当前意图、Complete 回传、物理执行/回滚以及 GameEngine 命令队列排空；这期间保留 UI、adapter、身体和碰撞资源。达到排空边界后才关闭 UI 与 adapter、调用 `CloseTrace`，再由 HostLifecycle 等待封存与音频释放后退出 SceneTree。回滚失败须进入失败封存记录，保留其锚点直到场地清理，不能以成功关闭替代恢复成功。

## 几何与资产

`RoomTerrainSkin.gd` 只读取现役场景 `terrain_rows` 的 `.DS` 格装配地面 TileMap；当前发布范围为 central_hall 和 granary。重绘层位于 Architecture 与独立物件之间，原镶嵌地纹用原图坐标保留，碰撞、门、站点和位置ID继续由原场景持有。`asset_pipeline/highlight-room-skins` 与章节地图生成器共用 `attach_skin`，避免再生成时丢失接线。

`Contracts/geometry_contract.json` 是路由、锚点、房间、站点、站位、出生点、出口与室外坐标的唯一发布合同，含 cell_size、maps 和 additions。房间提供完整执行 ID、region、rect 和精确 rects；出口同时声明 point、path、rect、target、spawn、internal_gate 和可选 gate。室外坐标使用 outdoor_origin/core/cores/origins。CampusMapView、WorldView 与地图页通过 GeometryCatalog 使用该合同，正式场景提供画面与碰撞实例。外部 living_hall 目标在发布时显式映射为 living_floor 及其带前缀出生点，运行端不重复推导旧别名。仅一个 region 的地图保留统一区域语义；多区域地图无匹配为 void。DV 只使用语义位置和锚点 ID。

GeometryCatalog 只负责运行读取与绑定。完整场景/合同校对函数位于 `../mcp/sandboxes/GeometryContractValidator.gd`，由 MapSandbox 在 bind_layout 前调用；校对逻辑不进入生产 Host。

运行资产放 `ShareAsset/`；生产源在 `../mcp/asset_pipeline/sources/`，生成器在 `../mcp/asset_pipeline/`。缺失源稿不等于运行资产可删除。场景和导入资源使用 Godot 原生资源；移除旧资产时同步移除该资产侧车，保留其他导入配置。

UI 使用 `Styles` 下 Theme 与命名变体，不在节点散落 theme_override 或临时 StyleBox。地图页只显示本人已获得的 map_knowledge、当前地点与实际公开的门选项；几何合同中的内部名字不会直接成为知识。

## 标题、案卷与呈现

TitleUI 与 DeskStage3D 使用真实 3D 桌面、相机、灯具和书体。BookProjection3D 把屏幕射线映射到页面 SubViewport；输入与显示共用相机合同。BookLeafTurn3D 使用冻结旧页纹理；翻页期间不按帧 GPU 回读。低动态设置使当前动画落定并恢复输入。BookSettingsPage 是设置页；旧 SettingsUI 场景不再使用。

Main 在地图、人物、碰撞、UI、物理注册与实际绘制完成后揭页。界面过渡由 UITransition 和 ModalPanel 管理；关闭后再派发捕获的合法空间选择。普通退出、窗口关闭和呈现绑定失败统一通过 HostLifecycle 等待异步关闭。

对白称谓只使用该条已交付的 speaker_name；内部 speaker 只用于资源映射。案卷只归档实际读到的观察者正文。个人草稿不授予事实。Main 保留 execution_event、source_id、source_field、行序号和 source_refs，公开的 presentation_id 贯穿对白与空间选项；panel 使用 dialogue / spatial / casebook 等实际界面身份。

## 诊断、控制台与演出样本

`scenes/Highlights/Highlights.tscn` 是独立条件节选舞台，读取 `Contracts/highlight_stages.json`，使用32px地形、独立道具和局部CharacterBody2D演出。它不创建正式会话，不授予观察、效果或通关；`--highlight=<id>` 选择六段之一。启动、内容cue、Y排序和局部搬运边界见 `scenes/Highlights/README.md`。舞台图像发布器为 MCP 的 `asset_pipeline/highlight-stages`，自动地形发布器单独维护地形连接与来源合同。

高光舞台的移动与搬运cue等待实际route_finished；在途关闭下一句，受阻停止后续旁白，交接后才登记接收。16×12画布整体近看，人物与32格比例不单独缩放。墙体使用16四边格式，地面和岸缘使用47角边格式；它们共享稳定source/terrain编号。

PlayerViewTrace 常开完整原始输入事件记录，通过 node_added 为新增控件接线。普通启动不逐帧扫描 UI 树；`--dv-audit` 或控制台 `audit on` 才开启逐帧扫描和可见记录，关闭审计保留事件输入记录。呈现样本使用 `kind: presentation`，并保留实际身体样本与原始输入。时序存储为 4096 条环，每次 PollTimings 最多消费 256 条；关闭清单记录丢弃量。

控制台 Inspector 只在页面打开时每 0.25 秒刷新 Physical、Mind 和按 score 排序的候选树。性能页使用固定 120 帧环，记录 frame、prepare、complete、navigation 和 physical 的真实毫秒耗时，并将阶段耗时归入实际完成帧；帧均值与峰值保留，不重复采集、绘制或平均 FPS。离开页面关闭诊断帧采样。DvSession 的会话累计统计独立于此开关；性能页每 0.25 秒只读同一测量截点的轻量摘要，顶部显示游戏小时／现实小时、正式决策数／现实小时及当前控制来源的接口小时／现实小时，同时注明真实采样时长和游戏推进量。细分引擎、投影、初始化和归档耗时独立显示，嵌套计算使用排他耗时汇总；关闭归档写 engine-performance 报告和二进制采样，导出失败记入关闭清单。时间口径见 `../docs/engine-performance.md`。

3D 书页记录表达页面纹理内容与冻结状态，不把 2D 控件可见性当作屏幕遮挡证明。`view_schema=3` 的 text_scope、visibility_basis 与 screen_text_visibility 明确此边界；实际遮挡证据由工具侧渲染帧承担。

F12 或反引号打开控制台。status / options 读取当前观察者投影；act 使用当前正式选项与空间接触。inspect / tp / time 需要 `--dv-dev` 会话，Core 负责开发修改。回放载入 dvdag / dvlog，Godot 与 CLI 使用 `dv_core/host_bridge/SmartReplay.cs`。ReplayStage 在独立 SubViewport 中组合 ReplaySession、Main、WorldView、StoryActor 和真实 3D 书页 UI；播放、暂停、单步与 1 / 2 / 4 / 8 倍速由演出时钟控制。

回放先消费记录的输入、事件回执和实际 trajectory，通过 WorldView 路径执行、StoryActor 碰撞及已记录门道坐标变换呈现移动，并以 action_event 去重。旧记录缺少轨迹时只按正式几何作语义重建并注明范围；缺少源位置时保留原文本，不补造移动。回放设置写入独立内存 ReplayPreferences，书签只展示记录清单，回放界面不打开系统文件选择器、不调用正式保存恢复，也不修改现场世界。

`--dv-mock` 使用标准演出包和 ReplayClock，提供案卷、表单、对白、门和地图样本，不创建正式 DvSession。独立纯 GDScript 入口为 `python ../mcp/godot_workspace.py --mock-ui`（从本目录执行）或统一 MCP 的 `ui_mock`，临时工程不创建 C# 项目。样本身份明确，样本不读取或写入正式世界。`--dv-auto-start` 跳过手动开始操作；不选择世界行动。正式参数包括 `--dv-controller=`、`--dv-viewer=`、`--dv-profile=`、`--dv-zone=`、`--dv-seed=`、`--dv-speed=`。播放倍率只改变宿主时钟与物理步频，不改写 DV 费用。

## 工具边界

测试与画面采集继承根 AGENTS 的替换规则：同一任务使用固定输出目录，每次新测试前核对绝对路径并递归清理旧测试输出，不保留轮次复制包。当前轮封存和失败记录仍须完整、如实；下一轮开始前统一替换。

开发工具位于 `../mcp/verification/`，地图沙盒位于 `../mcp/sandboxes/`。工具工作区由 MCP 接线到生产场景；游戏项目不包含 addons、tools、FrameArchive 或验证脚本。FrameArchive 的图像读取、编码与封存属于工具工程。现役工具路径和运行方式见 `../mcp/README.md`。

测试、检查、构建和探针只在用户当前授权范围内执行。未运行的项目保持未运行，不以源码修改宣称通过；验收结论必须对应实际运行证据，源码实现、工具输出、画面取证各自说明范围。


会话摘要统一使用 `game_hours_per_real_hour`、`decisions_per_real_hour`、`interface_hours_per_real_hour`，三个比率的分母均为 `elapsed_wall_ms` 对应的现实小时；分母为零或记录缺失时显示缺失，真实零推进可显示零。NPC 和自主选择归 engine，外部主体按明确的 human/llm 来源计数，只有正式状态新增的决策进入频率。人工／LLM 接口耗时记录真实决策请求交付至该请求输入被接受的区间，保留请求身份及取消、失败等实际结果；缺少区间不能用整个会话墙钟替代。引擎接口按实际计算调用计时。现实窗口从打开／恢复完成到关闭请求，GameEngine.CloseTrace 在归档前冻结该时钟；进行中的打开结束后再次幂等冻结。短窗口以真实采样时长呈现，不从播放速度或游戏时钟回算现实时间。报告保留主体决策、阶段拆分、存档与轨迹截点以及采样覆盖；统计不改变正式行动控制、行动完成状态或人物认识。

高光舞台的Space与数字键在输入入口消费完整按键事件；启动参数以SceneTree的一次性标记消费。诗人幕采用512×256局部镜头，其余保持512×384；角色和32px地形比例固定。接收台用发布地形材质装配，栏杆按脚点排序；最后源质cue等待人物高处站位后推进，工程说明见Highlights/README.md。
