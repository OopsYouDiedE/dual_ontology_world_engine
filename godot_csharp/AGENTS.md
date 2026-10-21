# Godot 游戏宿主维护

## 范围与依据

本目录负责 Godot 场景、渲染、物理、碰撞、输入、UI 与音效。工程使用 Godot 4.6.1 .NET、.NET 8，通过 `DualOntologyWorldEngine.csproj` 单向引用 `../language_core/Session/DuelView.Session.csproj`。DV 负责决策、报价、效果、观察、传播与结算；宿主只传递正式输入、执行现场意图并呈现观察者收到的数据。

用户当轮指令优先，其余工作、验收与证据要求遵循根目录 `AGENTS.md`。内容设计依据是 `../docs/design-standard.md`；工程文档不另立人物、谜题或剧情结论。调整本目录架构、合同、命令或排错方式时同步维护本文，历史任务与原始记录通过 Git 和任务目录溯源。

## 现役模块与入口

| 路径 | 职责 |
| --- | --- |
| `project.godot` | 主场景、自动加载、插件、输入映射与翻译资源 |
| `scenes/Host/Main.tscn`、`Main.gd` | 正式宿主、UI 总装、现场执行与观察者呈现调度 |
| `scenes/Host/GameEngine.cs` | 唯一 DV 会话持有者、后台串行命令队列、快照缓存、保存与计时 |
| `scenes/Host/FrameArchive.cs` | 后台 PNG 编码、二进制记录与原子封存 |
| `scenes/Shared/Field/WorldView.gd` | 人物实体、现场移动、局部输入、可见区域与房间舞台 |
| `scenes/Chapter2/Field/CampusMapView.gd`、`.tscn` | 统一地图适配器、几何、导航、站点与跨图转交 |
| `scenes/Chapter2/Maps/` | 现役地图场景 |
| `scenes/Story/StoryActor.gd` | 现役人物身体、精灵与动作呈现 |
| `scenes/UI/UISystem.gd`、`.tscn` | 汇聚子界面，以纯数据事件与 Main 通信 |
| `scenes/UI/TitleUI`、`SettingsUI`、`FieldHUD`、`InvestigationUI`、`DialogueUI` | 各自的 `.gd` 与 `.tscn`，分别负责标题、设置、HUD、调查与对白 |
| `scenes/Shared/Audio/`、`Settings/`、`Locale/` | 自动加载服务、设置及本地化 |
| `addons/dv_checker/` | DV 检查编辑面板；维护要求见该目录 AGENTS |
| `addons/rpg_asset_adapter/` | 编辑器资产适配器；子工具入口以其 README 为准 |

`Main.gd` 内部保留 `RuntimeDiagnostics`、`PlayerViewTrace`、`DvFieldExecutor`；`WorldView.gd` 内部保留呈现绑定、词汇、视野、提示、房间舞台、空间输入与导航辅助类。UI 只收发字典等纯数据，不直接持有 Core 对象；Core 不依赖 Godot 节点。完整语言及 Session 合同见 `../language_core/AGENTS.md` 与 `../language_core/Session/README.md`。

## 正式动作与后台调度

唯一动作链是 `Prepare → 现场执行/世界法则回执 → Complete → 呈现`。预测、推演和界面显示不发布真实行动效果。准备好的 `dv_action` 带执行主体的 `execution_snapshot`，只交现场适配器；观察者正文仍按本人的实际观察与收闻过滤。

`GameEngine` 串行消费输入，在后台完成打开、Prepare、Complete、快照构建及 JSON 到独占 Godot Dictionary/Array 的物化；工作线程不访问 Node、场景树或共享呈现资源，任务完成后才把数据图交给主线程。生产 `view` 事件自带 `snapshot`，Main 直接使用它；UI 把已完成的快照当作只读数据，编辑内容另作独立副本。`Snapshot()` 与 `ReadMilestoneProgressJson()` 均在 `GameEngine.cs` 中，只读取已完成的 JSON 缓存，不遍历工作线程持有的 DV 状态；前者保留兼容解析入口，生产视图事件不重复调用它。命令异常冻结继续分派，不能自动重试一个可能已经提交的转换。完成回执按 DV 行动标识去重，等待回执期间不重新分派意图。

`view_ready` 或 `dv_next` 启动下一步；显示完成不另行构造第二套 Prepare 路径。阅读期间 Main 可发送正式 `dv_next`：仅远离当前观察房间、且目的地也不进入该房间的 NPC，允许沿同一 Godot 执行器与 Complete 推进。玩家动作、同房动作和新的可见对白留在前台队列；正在阅读的正文与 HUD 不被后继快照替换。空呈现的下一调度提示只发一次，不预写回执、跳过碰撞或后台代选玩家动作。

阅读结束后，队列中的 `state` / `view` 等待仍在途的后台现场执行结束，再同步观察者快照，避免旧的人员投影重新取得实体并取消 NPC 的真实移动。该等待不阻塞现场回执或 Complete 的直接派发。执行器的单动作锁覆盖异步资源准备、实际移动和回执收集的整个期间。

Prepare 返回逻辑完成时刻已到达的单个意图，其他主体的排队动作由 DV 保存。`execution_cost` 是该动作完整费用，DV 对已等待时间去重结算；宿主不另推游戏钟。未完成的现场执行继续使用同一 pending。

## 新会话配置与播放倍率

`GameEngine.ConfigureSession` 接受 `{controller: "player"|"ai", viewer?: string, campaign?: record}`，只允许在打开会话前设置。默认主体、内容参数和资源绑定由 DV 提供。玩家候选与 AI 规划共用同一正式动作链，不在在途动作中切换控制方式。

Main 的导出属性以及 `--dv-controller=`、`--dv-viewer=`、`--dv-profile=`、`--dv-zone=`、`--dv-seed=` 参数配置同一入口。`--dv-auto-start` 只触发开始操作，不选择世界动作。正常游玩默认一倍速，`--dv-speed=` 可设置 0.25 至 8；`physics_ticks_per_second` 按 `60 × max(1, speed)` 设置，四倍时为 240，保持物理单步间隔。DV 分钟费用、预算和判断不随播放倍率重写；实测游戏时间不得用现实时间乘倍率代替。

普通游玩默认 `audit=false`，显式 `--dv-audit` 启用完整来源记录。普通模式仍保留推理实际需要的历史状态、输入、可见正文、计时及存档；关闭清单注明记录策略。性能采集工具固定开启审计，两种模式的证据和速度分别解释。

## 空间输入与现场执行

门槛 D 格的出发房间身份使用保留身体的连续脚点核对原膨胀障碍矩形；相邻导航格中心落在墙内不能单独抹去合法门槛脚点的出发身份。导航、目的地、站点半径及到达误差仍按原规则核对，不把门槛出发当作目标到达。

同房间内 WASD/鼠标行走由宿主寻路和真实碰撞执行，不提交 DV，不扣游戏时间或动作步数，也不产生观察、答案或成功交互。`awaiting_input` 更新现场资格表；玩家接近物件或人物后交互，单一动作直接提交，多动作在接触点显示局部选择。随身/等待操作仍由 DV 资格表准入。Main 在正式执行前复核人工热点接触。

跨房间仅提交 DV 已准入的移动 ID。鼠标点击门道时先按实际几何匹配唯一邻室并真实走到门口；E 可在近距门口发起同一动作，多义门不猜选。门道 D 格可用于出发核对，但不作为到达邻室或获得观察的依据。

同图房间也可共享没有 D 标记的开放地板边界。`CampusMapView.passage_at(source, attempted, from_room, to_room, walker)` 仅查询已载入 authored 几何：真实身体仍在 source、两点分别属于指定室内、地形为相邻的可行走地板格、探测不超过一个现有地形格，且原导航障碍与身体 `test_move` 均允许该短步时，返回 source/target/landing 证据。WorldView 只把该证据匹配到当前唯一可用的同图跨房动作；WASD 到边界、近距 E 与边界点击均保留 Prepare、真实碰撞及 Complete。该查询不造门、不改碰撞、站点或半径，不写位置、不授予房间或观察；远距地板点击仍先在源房真实行走。镜头显示边界与逻辑房间、准入及现场到达分别处理。

在途连续通行提示只投影真实 `continuous_request` 阶段和取消标记：准备、通行、取消、核对或返回源处。提示优先于普通热点/控制说明，不依赖 `can_submit` 再次开放；对白、模态界面、关闭及隐藏执行场仍抑制。三语正文不含内部调度术语，不创建行动、剧情或知识，不宣称到达；请求清除即恢复原现场提示。

E 的近门候选统一汇总同图 D 格邻室与跨图 `Exits/Shape` 矩形。`CampusMapView.manual_exit_targets_near(point, radius)` 只查询实际出口几何、armed 与通行值，不执行移动；`SpatialInput._near_door()` 只接受当前 `available` 的 doorway 选项，按行动 ID 去重，全部近门候选仅一个时才沿原正式链提交。现场可操作且 `spatial.can_submit()` 成立、无热点提示时，HUD 显示该候选的已交付 label 与 E；否则显示三语通用控制提示，不从隐藏状态补目标。模态与对白期间不显示该提示。新热点火花绘制复用 `site_visible`，低动态设置保留静态标记并停止闪烁时钟。

家具命中保留实际绘制与视野过滤，优先选择已有 `sprite_sites` 绑定且当前可见的现场物件，普通家具名称只作回退。静态“当前可交互”标记仅来自当前 `available` 的 site/person 候选，并同时要求现场 active、`spatial.can_submit()` 和 `site_visible`；该标记不表示 new、未取得或新观察，不改站点与接触半径。E 没有合法近热点或唯一门候选时，在可提交边界只显示三语 UI 短提示，不生成对白、世界事件或行动资格。

悬停与鼠标按下共用 `WorldView.pointer_hit(point)`，输入为现场本地坐标，保留站点可见性、绘制过滤和映射家具优先规则。鼠标按下在原 `_unhandled_input` 与 enabled/UI 门禁后，用本次 `event.position` 经现场 canvas 变换逆矩阵换算并立即查询；地面导航也使用同一点，不读取上帧 hovered 或另查鼠标全局位置代替该次事件。采集器同包输入不能以等待一帧替代生产命中合同。

人工同图连续过门因明确 `released` 取消时，只在真实源房、身体停止、空转交链且 executor 捕获的 Prepared 起点与实际终点精确相等的子集中生成 `continuous_cancel` 重试证明。WorldView 提供原生取消候选；Main 独立核对当前 Prepared 意图、请求、主体、行动、序号、两房、地图、实际脚点与 `interior` / `source-side-doorway` 源几何后，才签入 `source-cancel-v1`、`action_event` 及 `identity_verified`，移除来件自行声明的证明。DV 只授当前同签名的人工新输入重试资格；原动作仍 rejected，费用、位置与观察不改，不自动重发。宿主显示三语取消短提示，不宣称到达。请求时脚点另记 `request_source_position`，不能替代 Prepared 起点；实际返回但坐标不同、跨图、其他取消原因及持续受阻不属于该重试合同。

跨图及跨房动作真实执行一部分后失败，可能保留与 DV 拒绝结果不一致的实体地点；此缺口尚未修复。预备资源与入口校验不能代替最终站点成功，也不能保证动态受阻后仍在源图；不得传送身体、伪改保留实体记录或放宽源身份核对掩盖失败。涉及部分终点的正式合同须另行处理。

正式移动沿 `Main → DvFieldExecutor → WorldView.execute_move`，现场成功后才执行 Complete。回执核对主体、序号、行动、地图、逻辑房间、执行房间和显式站点，保留真实终点、站点范围与到达误差。`destination.room`、`execution_room`、`departure_station` 各司其职；依赖现场接触的观察只由成功 Complete 产生。缺少人物、地图、站点或合法出生点必须返回失败，不把命令行逻辑执行当作几何到达。

人物碰撞包含其他人物，不移动旁人让路，不传送至目标。站点接近核对原半径和房间，枚举合法候选并避开已占终点。连续脚点与导航格中心共同核对原障碍膨胀范围；保持显式站点原半径和 4 px 到达误差。普通房间终点依次选择可达空闲 Stand、具有转身空间的房间格及其他合法房间格，各组按实际距离排序。

鼠标路线与 StoryActor 每个物理帧按原速度乘 delta 分配移动距离，航点结束后继续消费剩余距离，每帧最多处理 32 段，每段经过 `move_and_slide`。只合并同方向共线点，保留拐角、站点、速度、碰撞与受阻判定；不以直接改位置补足最后一步。局部房间的可走范围写入独立 AStarGrid2D，不修改地图共享的基础导航网格。房间候选索引只缓存静态几何，实时占位仍按真实主体位置核对。

初次物化使用 DV 声明的 `people.initial_anchor`；地点移动锚点不自动变成所有人的出生点。缺初次锚点时只选既有、同房、可走且未占用的 Stand/Spawn。切图沿用已存在的真实实体与终点。室外到达按实际地图边界及可走格判断。

离屏执行使用独立 World2D 中的同一地图、导航与碰撞逻辑。`DvFieldExecutor` 持有唯一人物实体，并按原资源、地图、坐标及碰撞形状提供占位查询；其他场地为同图人物建立不带脚本、渲染或 DV 状态的静态占位体，随真实位置刷新。执行、换图与实体转交后更新占位；出生点受阻时保留源图并返回受阻，不移动原实体。真实状态、已执行动作和尚未绘制的正文分别留痕。

相同人物与场地的静态占位体复用，实时同步实体变换、碰撞层与形状；只在主体不再属于该资源/地图、实体转交或场地关闭时撤销，避免每次快照重建一批碰撞节点。

## 地图、呈现与资源

生活翼同层统一使用 `Maps/living_floor.tscn`，由 `tools/build_living_floor.py` 组合五张已发布场景生成；原场景和纹理保留为生成输入。四寝区隔离门是同图物理屏障，使用 DV 提供的各区放行值，不触发切图。逻辑房间标识保持 `zone_*:*` / `living_hall:*`，执行地图统一为 `living_floor`；室外分区及不同建筑不合并。公共厅中段补接六格以避免原局部底图相互重叠，门、标记、碰撞与灯光同步变换。

地图源可通过 `stand_capacity` 声明各几何区域的站位容量。`tools/build_chapter2_scenes.py --stands-only <地图...>` 保留原有 Stand 顺序，只从原障碍、门道及邻格过滤后的候选补齐不足；发布前复核已发行标记与源配方一致，只更新 Stand 标记，不重绘地图或改动碰撞。生活翼随后使用 `tools/build_living_floor.py --stands-only` 同步新增标记并保留其他已发布内容；运行时仍只使用已发行的合法 Stand/Spawn，不临时移动旁人或放宽出生规则。

实际盲测可通过 `profile_dv_capture.py --controller player --window-mode offscreen` 启动真实 Main 场景，使用保存的渲染截图及 `input.json` 瞬时输入包操作普通视口输入。默认离屏且不可聚焦，保留图形渲染与物理，不使用 headless 逻辑运行替代；独立盲玩代理只读取该轮画面与通用输入协议。`--window-mode window` 可恢复可见窗口。

`dvg_snapshot.geometry_bindings` 提供地图站点、道具、纹理、位置与 footprint。`CampusMapView` 按声明建立 Marker2D、Sprite2D、碰撞与导航；相同声明不重复重建。场景和动态呈现均由内容声明驱动，宿主不以解析剧情动作 ID 替代空间绑定。

`CampusMapView.prepare_map(map, snapshot, tree)` 异步准备资源、几何与导航，返回 false 表示不可用或关闭取消，不表示移动结果。Main 在首次可见/离屏 setup 前传入自己的 SceneTree 并等待；跨图执行也等待准备后重新核对源实体、源图与真实入口。`FieldResources.gd` 通过线程加载请求准备资源，以有限缓存和批内强引用管理生命周期；Node 实例化及几何编辑仍在主线程。每个适配器按三个地图布局的上限淘汰空闲缓存，当前显示和在途准备的布局保留到交接完成。房间候选索引按约 2 ms 预算分帧准备；`prefetch_snapshot` 只在可见场地预备邻接地图，隐藏场地按正式行动需要准备，不改变当前地图、通行资格、主体归属或 DV 状态。实际实体、占位和可见淡入完成后才由 `finish_transition()` 释放切换状态。关闭取消等待并释放离树布局，已排队的 access 刷新拒绝关闭后清空的记录。

`ShareAsset/Internal/Props/Chapter2/manifest.json` 是运行时路由、出生点与尺寸输入。`CampusMapView` 根据建筑底图基名动态载入 `rooms/*.relief.tres`，这些 CanvasTexture 依赖相应 `*-normal.png`。动态路径与资源侧车都属于依赖检查范围，不能仅按场景中的字面引用删除资源。

场景基础格为 32×32，角色基础设计格为 32×48。正式人物优先使用 `Characters/PixelCast/<key>.tres`，缺失时按现有合同回退至 AnimeCast；对白使用 DialogueCast 立绘。共享家具和证物仍分布于 `FirstAwakeningInterior` 与 `AwakeningEvidence`，它们由现役统一地图消费。资源职责、来源与重建前提见 `ShareAsset/Internal/Characters/README-zh.md` 和 `ShareAsset/Internal/Props/Chapter2/README.md`。

重绘三向六姿素材可使用 `tools/publish_redrawn_cast.py --source <源目录> --review <新审阅目录>`。原画中 alpha>=200 定义正式二值覆盖，边界检查与发布使用同一覆盖定义；整批检查后才替换 PNG。沿用现役 .tres 的单元、姿态顺序、动画费用和节奏，每个人全姿态使用同一缩放并对齐原脚点；不改变碰撞与人物状态。审阅目录保存像素预览、原画/旧资产/新资产指纹。原有 bounds(path) 默认阈值保持不变，新发布器显式选择覆盖阈值；这属于美术覆盖规则，不是剧情或物理验收规则。

运行时资产放在 `ShareAsset/`；若提供高分辨率源稿、切片工程等生产输入，放在受 `.gdignore` 保护的 `AssetsSource/`。当前 Git 树并未包含全部历史生产源和配方，重建前必须核对输入实际存在；缺源时保留现有运行资产并报告缺口，不把它们当可随时重生的缓存。生成文件从对应工具重建，不直接修改成品；调整路径同步核对场景、动态资源路径、插件配置及生成工具。删除资产时只移除该资产对应的导入侧车，保留其他 `.import` 的导入配置。

UI 样式、颜色与排版统一由 Theme/StyleBox 等 Resource 维护；不在节点中随意添加 `theme_override_*`，也不在脚本中临时覆盖主题或新造样式盒。非原生二进制素材的 Resource 使用文本 `.tres`。现役 `DialogueTheme.tres`、`TitleTheme.tres` 与 `OverlayTheme.tres` 均由 `tools/build_ui_theme.py` 生成；共有字体和纹理属于其依赖，不与未使用的主题分支一起删除。对白、调查纸页、HUD 与独立推演手稿的版式和接口维护在 `../docs/ui-presentation.md`。普通阅读区使用命名主题变体与轻规线，避免重新引入默认控件外壳或闭合重框。

标题桌面采用 `TitleUI.tscn` / `TitleUI.gd` 与 `DeskStage3D.gd` 的真实 3D 实现：桌面、书本、灯具及摆件使用真实几何，在同一 `Camera3D`、同一空间尺度与光照下渲染；火焰、灯光能量与受光材质必须使用一致的闪烁驱动。透视、遮挡、接触关系和投影由共同场景承担，不得用相互独立的屏幕贴片伪造不同视角的物体。`BookProjection3D.gd` 负责显示和输入的共同相机契约，将屏幕射线与书页平面的交点逆映射到 `580×820` 的页面 SubViewport，并把真实鼠标输入交给原生 Control；相机偏移、窗口缩放与视觉位置变化时必须保持命中一致。

`BookLeafTurn3D.gd` 及 `book_leaf_mesh_3d.gdshader` 提供具有独立正背面、真实厚度和边缘的页面网格；硬书皮使用 rigid-cover 行为，纸页允许曲面弯折，书脊位置、翻页方向与下层页面状态保持一致。旧页内容通过独立 SubViewport 的 `UPDATE_ONCE` 渲染后冻结，生产翻页路径不得逐帧执行 GPU 图像回读或重建页面纹理；截图工具为记录证据而读取图像不属于生产动画路径。`reduce_motion` 开启时直接落到目标页面并结束当前动画，恢复正常输入，同时停用视差及明显闪烁，不得留下半翻书页或输入锁。

标题材质按颜色与表面数据分别维护：当前 `reference-material-atlas.webp`、`reference-cloth.webp`、`reference-paper.webp` 为参考图辅助生成的颜色素材；`tools/build_desk_surface_maps.py` 使用 Blender CPU Cycles 从独立微表面节点烘焙 `reference-normal.png`、`reference-roughness.png` 及布面/纸面裁切图。法线使用 OpenGL 切线空间 +Y，粗糙度使用线性数据，二者不按颜色纹理转换。静态书体与翻页网格保留切线，法线框架随页面变形和背面 UV 同步；UI 字样不参与表面高度生成。`BookSurfaceDetail3D.gd` 提供静态书体倒角，`paper_mount.gdshader` 仅修饰纸签背景边缘。玻璃使用透明材质和场景反射，能力边界与资产来源写入对应 AssetsSource README。

桌面实物比例的唯一输入为 `AssetsSource/Internal/Props/UIDesk/Painterly3D/desk_physical_spec.json`：记录书封毫米基准、实物规格来源、各道具目标尺寸和布局。几何生成器按实际网格顶点标定，输出 `ShareAsset/Internal/Props/UIDesk/Painterly3D/desk-physical-layout.json` 的测量尺寸和灯芯顶点；`DeskStage3D.gd` 从该配置读取灯芯与火焰尺寸，不另写独立灯光位置常量。改变道具体量时同步核对落桌、整段翻页扫掠、屏幕遮挡及反射体积。相机俯角与取景距离独立维护，拉近镜头不得隐含降低俯角；按照设计规范指定的俯角保持近景，物体放大时不得拉远或扩大视野来强行容纳全体道具。构图通过实际摆放与自然裁切调整，开书只平移跟随阅读页。按钮射线激活成功不能替代画面遮挡检查。

`tools/build_painterly_desk.py` 使用 Blender 4.2 生成桌面几何；可编辑源稿保存到 `AssetsSource/Internal/Props/UIDesk/Painterly3D/painterly_desk.blend`，运行资源为 `ShareAsset/Internal/Props/UIDesk/Painterly3D/desk.glb` 与同目录 WebP 材质。源稿与运行资源分开管理，运行场景不依赖 Blender 在线执行。`tools/capture_title_3d.gd` 是当前 3D 标题界面的观察入口，必须使用有可见渲染窗口的 Godot .NET 构建，并预先完成 C# 编译；它仅实例化生产 TitleUI，通过真实输入检查页面与按钮，输出 PNG 和二进制 `ui-input.dvlog`，不加载 Main、不启动 DV 会话，不能据此宣称游戏或存档恢复已验收。`tools/capture_title_book.gd` 保留为旧 2D 入口，其结果不代表当前 3D 标题界面。上述为实现与验证契约，验收状态以实际运行证据为准。

以下既有载入与路径恢复约束保留原文，未因本次标题界面工作重新验收：

续读列出 user://saves/*.dvsave，也可显式选择文件，向 Main 发 load/path；GameEngine 在原串行队列用 DvSession.RestoreFile 恢复，拒绝尚待现场回执的存档。文件验证失败不安装会话，load_error 返回书页；不得据此声称跨版本与现场恢复已验收。

`UITransition.gd` 管理可取消的进入/退出状态，过渡使用现实时间，不受游戏播放倍率影响；`reduce_motion` 修改实时生效并使当前过渡落定。`ModalPanel.gd` 负责打开、退出期间的输入锁、焦点圈定/恢复和完成信号，实际退场后才发 closed。UISystem 串行切换设置、调查及其各面板，上层弹窗阻止底层对白或现场消费同一输入；现场选择在弹窗退场后发回原 site 与 choice_id。Title 的设置层高于标题层；游戏内视图替换等待原 UI 的 `transition_out`，卷首交接遵循下述揭页契约。UISystem 提供可等待的 `transition_in/out` 与 `fade_in/out`，屏幕遮罩覆盖期间保持 busy；WorldView 只在目标准备完成后进行可见跨图淡变。

对白、回看、提示各自取消旧任务；空对白与立即完成的低动态效果也提供可等待的完成结果。换行保留正文顺序与来源，立绘通过线程请求预备后绑定，旧请求不覆盖新说话者。重入、关闭或新提示不得遗留能隐藏当前界面的旧回调。

`DialogueUI` 的当前显示及回看称谓只使用该条已交付的 `speaker_name`；缺失或为空时留空，不回退内部 `speaker` ID，也不从旧条目缓存补名。内部 `speaker` 仅可继续用于立绘资源映射等非称谓用途；原始逐行来源字段仍照实保留，不能由显示层替另一观察者补出人物身份。

`RoomStage` 负责当前房间构图、局部灯光与有上限的粒子；离屏执行不创建舞台特效。`reduce_motion` 关闭微尘及缩放动画。房间构图、着色器和灯光不改变碰撞、门洞、DV 目的地或人物认识。对白保留原透明立绘与滚动正文、原文顺序、说话者映射和用户继续操作，不能把界面播放完成当作行动成功。

Session 呈现保留 surface/lines 和 `source_refs`。Main 为发出的正文记录 `execution_event`、`source_id`、`source_field`、`emitted_line_index`、`paragraph_index` 与 `line_id`。过滤后的显示序号不冒充源行号，缺少来源不补造；`PlayerViewTrace` 在实际 `frame_post_draw` 后记录首帧，待显示正文与已绘制正文分开。

## 卷首加载与揭页

开始阅读和标题续读立即发出正式请求，并把当前书页保留到现场真实就绪。`Main._view` 在标题后装配 `WorldView` 与 `UISystem`，等待地图、人物、已请求立绘、视野光效、镜头、HUD 和实际绘制帧，再调用 `TitleUI.transition_to_field` 单次揭页；不能先翻到占位页或撤去标题后才装配。`_opening` 与 `UISystem.host_input_blocked` 覆盖整个准备和揭页期间，低动态仅省略运动。游戏内视图替换保留其屏幕遮罩。接口、错误边界及二级观察入口 `tools/capture_start_reveal.gd` 见 [卷首加载与揭页](../docs/title-opening.md)；其截图不能代替剧情验收或同条件性能比较。

## 共享书本、案卷与界面设置

`CasebookUI.gd` 在游戏内实例化同一 `TitleUI.tscn`，设置 `mode = "casebook"` 并提供 `CasebookData`；卷首使用 `mode = "title"`。这两种入口共享 `DeskStage3D`、实体书、翻页材质、页面 SubViewport 与 `BookProjection3D` 的原生输入路径。`BookSettingsPage.gd` 提供共同设置页，`BookArchivePage.gd` 提供目录、记录、详情及个人批注；不要另造一套屏幕贴片或游戏内设置界面。翻页、焦点、文本编辑、窗口适配与减少动态设置须遵守同一物理书契约。

三份现役主题共用生成器中的 paper / ink / hand 语义色与字体角色：`TitleTheme` 负责卷首和案卷书页，`DialogueTheme` 负责对白，`OverlayTheme` 负责 `FieldHUD` 与独立 `InvestigationUI`。现场报头、纸页边距、正文、个人笔迹、选择行与焦点状态均使用主题 variation；不得在场景或脚本中另造局部配色与字号。生成与 `--check` 必须覆盖三份主题，补齐 Overlay 样式时不得顺带改写 Title / Dialogue 的设计。

三语界面使用 `GameLocale` 的 `zh_CN`、`ja`、`en`，偏好保存于 `user://language.cfg`；音量、显示及辅助功能由 `GameSettings` 保存于 `user://settings.cfg`。它们只影响宿主呈现，不进入 Core、正式会话、世界存档或案卷侧车。切换语言只翻译界面文案，不改写已收到的原始正文、人物发言或玩家批注；刷新时保留当前编辑内容、焦点与滚动位置。

`CasebookData.gd` 是按 viewer 隔离的纯 UI 数据投影，导出结构为 `{version: 1, viewer, entries, notes, next_note}`。来源限于 `DialogueUI.line_read` 已交付的正文行、实际打开的可见卡片以及显式提供且标记 acquired 的案卷记录；不能扫描 `snapshot.people` 等几何投影补出未见人物，也不能以隐藏的行动标签替代条目标题。看过物件与拥有物件分别记录，显示或收录本身不授予世界物品、行动资格或新知识。

正文条目保留原始 `lines` 数组及 `line_id`、`source_field`、`emitted_line_index`、`paragraph_index`、`execution_event` / `trace_id`、`source_id` / `source_refs`、说话者等已交付字段。列表摘要不能替代逐行来源；缺失字段不补造，显示序号不冒充源行号。图版及立绘只沿已经交付的可见卡片或明确取得记录引用资源，不按剧情编号解锁素材。

个人批注使用 `personal = true` 的独立 notes 记录，保留原文、关联条目、时间和地点，与证词、证物正文明确区分。案卷中的推演入口交给独立的 `InvestigationUI`；批注可以作为玩家草稿显示，不自动成为正式答案、世界事实或行动提交。正式调查、预测与行动仍走既有纯数据 UI 事件和宿主链。

`CasebookUI` 保存并恢复打开前的暂停状态，以 `reading_changed` 告知 Main 阅读占用；保存或切换书签期间保持交互锁。载入后 Main 必须清除旧视图队列、释放旧 UI 与现场执行器绑定，并隔离旧的异步呈现回调，待新快照装配后按既有 `view_ready` 链恢复调度。书本打开、关闭、换语言或编辑笔记均不得额外派发世界动作。

## 保存、计时与证据

Main 的呈现队列按约 2 毫秒批次跨帧排空，保持全部事件的原顺序；单个事件可能超过预算，不能把此预算当作帧率保证。排空期间仅锁住新的现场操作，当前对白输入和已接受的物理执行照常完成。新开局及标题读档共用播放配置，`--dv-speed` 限于有限的 0.25–8，物理步频为 `ceil(60 × max(1, speed))`，每帧步数上限为 `ceil(8 × max(1, speed))`；读档不改写保存的控制者。

存档辅助实机入口为 `tools/run_saved_game_capture.py` 与 `tools/capture_saved_game.gd`，配置、证据、输入与失败处理见 `tools/SAVED_GAME_CAPTURE.md`。正式新开局、恢复诊断与玩家 UI 输入诊断分列；请求检查点必须在采集截止前，实际保存以边界落定时间为准。

`input_gate_state` 在实际绘制后的输入门状态变化时记录 UI 占用、阅读、提交锁、待执行主体/动作、现场任务、地图切换和队列长度。它只诊断“强制等待”，不修改准入、调度或输入锁；与实际输入、Prepare/Complete、现场执行和玩家截图配对解释。

显式保存/恢复拒绝现场 pending，异步维护先等待后台任务并再次核对。关闭允许保留真实 pending，等待在途工作后分别封存输入、可见正文、计时、会话及存档。每项输出独立捕获异常，一项失败仍尝试其余项；先写 `.partial`，成功才发布正式名，失败保留部分文件。Session 的写接口自行发布，宿主不重复改名；关闭链只调用一次 SessionClose。证据目录独占创建，不覆盖既有记录，异常也释放关闭等待。`TraceClosed` 只表示关闭结束，完整性依 `close-result.dvlog` 中各阶段结果判断。

新运行记录使用带校验的二进制格式：输入、可见正文、计时、采样、帧索引、关闭回执为 `.dvlog`，原始会话为 `.dvdag`，存档为 `.dvsave`；保留全部字段、精确数、顺序及共享不可变节点。JSON 只用于进程内消息、编译配置和不承载运行轨迹的小型工程元数据。旧 JSON 记录仅只读兼容，不能成为新轨迹格式；含逐段原始记录的分析输出同样写 `.dvlog`。

`GameEngine` 用 Stopwatch 单调微秒记录 interaction_id/command_id 关联的排队、Prepare、快照、现场执行、Complete、绘制首帧与阅读结束；局部行走单列真实起止和游戏钟。`PollTimings` 消费在线副本，不清掉封存用完整记录。缺段保留未知，CPU、阅读、等待和实测游戏时间分别解释。

阶段观察器按每个 Prepare/Complete 命令重新绑定，记录 thread_id；投影、JSON、Godot 物化及主线程交接分别计时。同一命令可有 intent、viewer、executor 多个物化段，分析应按 command_id、阶段及 details 配对，不以一个 interaction 内首次同名阶段合并多个动作或快照。

`RuntimeDiagnostics` 在实际 `frame_post_draw` 记录较长绘制间隔、位置变化与节点视口命中。视口命中不代表穿透遮挡可见或人物已经认出对象。脚本/GPU 与 .NET 栈分别封存；墙钟重叠不等于 CPU 归因，未知残差、未闭合阶段、采样缺口及分析器自身开销均如实记录。

`FrameArchive` 只接收主线程复制的 RGBA8 像素，后台串行完成无损 PNG 编码、哈希及原子写盘，不调用 Godot 场景/资源 API。PNG 成功后才追加帧索引，最多八个未完成图像任务；队列满则记录 backpressure 与采样缺口，不阻塞主线程或默默丢图。主线程读回/复制与后台排队/编码/写盘分别计时。后台同序追加已序列化记录，文本不能静默丢弃；关闭先记录真实游玩终点，再排空任务、完成通知及其追加的索引，封存尾部时间单列。

### 案卷与书签持久化

Main 在调用书签保存或载入接口前复核宿主呈现边界：正文阅读、待交付事件/延后输入、队列排空、地图准备/切换、现场执行及已提交但尚未返回的 Complete 投影均须落定。载入或书页已打开时的请求若未满足边界，仍提示先读完正文并等待现场动作结束，记录 `bookmark_boundary_rejected`。书页未占用时可排队请求保存：`_bookmark_requested` 仅暂存下一次 `dv_next`，既有现场动作、Complete 和正文继续完成；宿主边界与 C# `IsBookmarkBoundaryReady()` 同时就绪后才进入原保存接口。暂存的下一次调度在获得安全边界后转入原延后输入队列，保存成功或失败后恢复；关闭、错误及换会话时清除旧请求。不能仅凭 DV 无现场 pending 就保存尚未读出的正文。`_completion_pending` 从真实回执安排提交起保持到 `dv_present` 或错误回传，覆盖清空现场意图到后台 Complete 返回之间的空窗。

保存使用独立 `_bookmark_saving`，与载入锁共同驱动现有书本操作锁；操作期间不关闭、重入或再提交书签，暂停状态保持。书签成功/失败事件在 `_dv_events` 直接处理，不进入被阅读占用挡住的呈现队列；事件解除对应操作锁，延后输入在阅读及操作锁均释放后恢复。已接受的 `dv_complete` 回执保留送达通道，C# 仍自行等待既有工作并复核命令/现场边界，不由 UI 锁代替。

宿主发出 `bookmark_loaded` 后，`_view()` 仍须明确返回现场装配是否成功；`_install_bookmark` 只有在成功且载入 generation 未变化时才解除操作锁并发送 `view_ready`。资源或地图准备失败时保留操作锁、暂停旧现场，记录 `bookmark_view_failed`，以 `PROCESS_MODE_ALWAYS` 的错误对话框说明“宿主已载入、现场未装配”，显示载入前备份路径并提示关闭后从续读重新选择备份；不假装回滚旧会话，不继续派发动作，也不另建回退会话。

`PlayerViewTraceInner` 使用 `PROCESS_MODE_ALWAYS`，Main 在卷首或现场子树装配后把它移到最后一个子节点，使只记录、不消费输入的 `_input` 先于书页的消费路径收到按键和鼠标。暂停期间的真实输入与控件激活分别留痕；记录节点不得调用输入 handled 或代为派发行动。

退出由 `Sound.quit_cleanly()` / `GameAudio.quit_cleanly()` 统一协调：Main 的菜单退出与窗口关闭都进入该路径，不另行调用 SceneTree.quit。GameAudio 先启动全部 lifecycle_hosts 的 `begin_shutdown()`，再等待支持该接口的宿主 `wait_for_shutdown()`，最后排空音频并退出。Main 的 `begin_shutdown()` 保持幂等且不等待，只启动唯一的 C# `CloseTrace()`；`wait_for_shutdown()` 先调用它，若 `_shutdown_finished` 已置位则立即返回，否则等待 `shutdown_completed`。`TraceClosed` 回调先置完成标志再发信号，避免信号先到导致永等。等待关系不反向依赖音频退出；C# 异常也须结束关闭等待，实际封存完整性仍以 `close-result.dvlog` 各阶段结果为准。

`scenes/Host/CasebookPersistence.cs` 是 `GameEngine` 的 partial 实现，不引入第二个会话或另写世界存档。主线程只接受可序列化的纯数据字典，按条目、批注及逐行来源白名单保留字段；Node、Resource、Callable 和设置/语言偏好不进入记录。工作线程只消费独占数据或不可变字符串，不访问 Godot Node 或共享 Dictionary；完成后在主线程物化回传数据。

| 宿主接口 | 合同 |
| --- | --- |
| `UpdateCasebook(Dictionary data)` | 更新当前 viewer 的 UI 案卷缓存，供关闭保存使用；不推进或修改 DV。 |
| `ReadCasebook()` | 返回独立字典，调用者不能借此改写宿主缓存。 |
| `SaveBookmark(Dictionary data)` | 等待既有工作后复核维护锁、关闭/故障状态、静止命令队列及无现场 pending，再调用当前 Session 的 `WriteSave`。 |
| `LoadBookmark(String path, Dictionary currentData)` | 先用 `DvSession.RestoreFile` 验证候选并拒绝 `pending.status = prepared`；安装前为当前会话写入全新世界备份和绑定案卷。 |

手动书签写入新建的 `user://saves/<UTC>-bookmark-<唯一标识>.dvsave`；载入前备份使用 `<UTC>-before-load-<唯一标识>.dvsave`。保存必须沿用 Session 的 `.partial` 到正式名发布契约，拒绝覆盖旧文件。常规忙碌、无效文件或侧车失败通过 `bookmark_error` 返回可读信息，不把它们转成全局故障停机。`bookmark_saved` 返回 path；`bookmark_loaded` 返回 path、backup_path、snapshot、casebook 和 casebook_warning。错误记录中的 save_written / casebook_written 区分世界保存与案卷保存，不能把前者成功写成整对文件均成功。

同名 `<basename>.casebook.dvlog` 使用现有 `BinaryEvidence` 写入单个二进制封装记录：`{version: 1, viewer, save_sha256, casebook}`。SHA256 绑定实际 `.dvsave` 字节，读取时验证二进制头、版本、记录数量、外层与案卷内层 viewer 以及哈希；不凭文件名认定归属。旧存档没有侧车时允许恢复空案卷；损坏、错配或异 viewer 的侧车不导入，返回警告。正式会话恢复仍以 `.dvsave` 为准，不从案卷重建世界。

游戏内切换前须成功完成当前会话的全新备份与配套案卷；若世界备份已写入而侧车失败，保留备份及失败证据，当前会话不切换。候选有效、备份完成且宿主门限再次通过后才调用 `InstallRestored`。初始标题 `load/path` 同样读取可选的绑定侧车，并随 `view` 事件交付 casebook / casebook_warning。`CloseTrace` 在原有自动 `save.dvsave` 成功后追加 `save.casebook.dvlog`，侧车失败独立记入关闭结果，不吞掉其他证据阶段，也不覆盖或删除已产生的世界保存。

## 工程命令与验证边界

从仓库根目录执行：

```powershell
dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers -p:NuGetAudit=false
python language_core/tools/check_dv.py --format json language_core/60-game.dv
python godot_csharp/tools/build_ui_theme.py --check
```

构建可用 `-p:DvPython=<Python路径>` 指定生成器。共享 `NuGet.Config` 使用 nuget.org，不提交个人机器的绝对包源路径；离线包源按开发者环境显式配置。通过环境变量 `GODOT` 指定 Godot .NET 程序；重新导入与运行命令为：

```powershell
& $env:GODOT --headless --editor --path godot_csharp --import
& $env:GODOT --path godot_csharp -- --dv-controller=player --dv-speed=1
```

只在任务确有需要且符合当轮授权时启动工程探针、采集或游戏。现役 `tools/dv_host_startup_probe.gd` 是短启动排错；`tools/profile_dv_capture.py` 包装 `tools/dv_level3_capture.gd`，记录实际输入、画面、计时与 .NET 采样。其 `--native-profiling full` 显式启用原生控制台分析，默认 off；`--speed` 默认 4，一倍复测显式设为 1。`tools/encode_dv_capture.py`、`report_runtime_hitches.py`、`summarize_interaction_timing.py` 消费已有记录；工具参数与格式以现役源码及 README 为准，缺失的旧探针不重新列为入口。

`tools/capture_casebook_ui.gd` 是共享书本的独立 UI 采集入口，使用经批准的有限可见正文/图版夹具，不运行 Main、DV 或 Core。它记录真实投影输入、页面与原生控件呈现，不能证明正式观察者实际取得证物、个人批注产生世界效果、完整书签恢复或剧情行为。夹具范围、语言临时切换、输出及期限见 `tools/README.md`；构建、采集和玩法验收分别报告，未执行的检查不得写作通过。

禁止执行根目录列明的流程回归，不能以断言数量、通过率或“全绿”评价游戏。构建、语法、生成物一致性仅用于工程排错。一级是无窗口正式 DV 逻辑输出；二级 UI 检查实际截图；三级只有明确要求最终效果时执行，默认由引擎侦探提供正式输入。独立盲玩需要用户明确指定，操作代理只看本轮允许的画面和输入资料，不读设计、源码或隐藏诊断，不预写路线和答案。具体时限与倍率按当轮约定，不沿用历史任务窗口。

完整行为结论依新正式会话的实际输入、可见结果和回执；图片、正文、原始事件、计时和来源分别保存，未发生/未覆盖/记录不足分别说明。不得放宽预算、正确条件、碰撞或计时规则制造提速，不把四倍播放、精简审计、纯几何夹具或短启动当作剧情通关和同条件性能改善。先完成当轮整批修改，再按要求统一排错和冻结；中途改源则结束该批次并保留失败证据。


## 人工重演与可见记录扫描

InvestigationUI 的假设表单由当前 DV replay_input 提供，所有选择初始未选。UISystem 与 WorldView 只拆出原 action_id 核对当前选项与接触，再沿原字符串链保留整个纯数据封装；不在 UI 运行模型或判定答案。Session 边界解析后由 DV 重核候选和正式执行。

PlayerViewTrace 每个实际绘制帧仍完整检查文本与可见对象；一次同步扫描共用只读的小型界面上下文，向可见记录添加字段前独立复制，不减采样、不跨帧缓存、不改变输入控制激活记录。扫描优化与插桩采集开销需分别实测，不按代码改动推断帧率收益。

## 共享输入工程索引

右键／B 逐层返回、手柄导航及 `GameSettings` 控制绑定迁移见 `docs/chapter1-input.md`。`UIInput.gd` 仅负责宿主呈现；现场移动沿既有 `move_*` 路径。`tools/probe_ui_input.gd` 使用隔离用户数据与原生 Controls，输出二进制输入观察，不运行 Main／DV，也不证明物理手柄、书页画面、地图或剧情验收；实际状态以本次运行日志和证据为准。

## 连续过门与开卷宿主

Main 的初始视图在不透明 TitleUI 下完成 prepare_map / prepare_presentation(initial)、视野与 UI 装配及真实绘制屏障，沿用现役 title-ready 纸页揭幕；现场 host_input_blocked 与 field.ui_busy 在揭幕完成前保持关闭，不重新接入旧 BookOpeningPage 启动状态机。视图失败停止事件消费并在书页报告，已开始会话不可原地重复新建。连续跨门请求仅在可用玩家选项与真实源侧门道内发生，宿主独立核对 prepared 身份后允许唯一边界，仍走正常物理回执与 Complete。缓存失效、取消、界面接口与性能证据边界见 `../docs/chapter1-continuity.md`。


连续过门的 continuous_request 在权威拒绝后的真实回源期间（含受阻重试）仍非空；书签边界必须等待它清除，不能因 Complete 已结束而保存或切换尚未协调的身体位置。
