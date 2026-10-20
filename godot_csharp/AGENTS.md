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

正式移动沿 `Main → DvFieldExecutor → WorldView.execute_move`，现场成功后才执行 Complete。回执核对主体、序号、行动、地图、逻辑房间、执行房间和显式站点，保留真实终点、站点范围与到达误差。`destination.room`、`execution_room`、`departure_station` 各司其职；依赖现场接触的观察只由成功 Complete 产生。缺少人物、地图、站点或合法出生点必须返回失败，不把命令行逻辑执行当作几何到达。

人物碰撞包含其他人物，不移动旁人让路，不传送至目标。站点接近核对原半径和房间，枚举合法候选并避开已占终点。连续脚点与导航格中心共同核对原障碍膨胀范围；保持显式站点原半径和 4 px 到达误差。普通房间终点依次选择可达空闲 Stand、具有转身空间的房间格及其他合法房间格，各组按实际距离排序。

鼠标路线与 StoryActor 每个物理帧按原速度乘 delta 分配移动距离，航点结束后继续消费剩余距离，每帧最多处理 32 段，每段经过 `move_and_slide`。只合并同方向共线点，保留拐角、站点、速度、碰撞与受阻判定；不以直接改位置补足最后一步。局部房间的可走范围写入独立 AStarGrid2D，不修改地图共享的基础导航网格。房间候选索引只缓存静态几何，实时占位仍按真实主体位置核对。

初次物化使用 DV 声明的 `people.initial_anchor`；地点移动锚点不自动变成所有人的出生点。缺初次锚点时只选既有、同房、可走且未占用的 Stand/Spawn。切图沿用已存在的真实实体与终点。室外到达按实际地图边界及可走格判断。

离屏执行使用独立 World2D 中的同一地图、导航与碰撞逻辑。`DvFieldExecutor` 持有唯一人物实体，并按原资源、地图、坐标及碰撞形状提供占位查询；其他场地为同图人物建立不带脚本、渲染或 DV 状态的静态占位体，随真实位置刷新。执行、换图与实体转交后更新占位；出生点受阻时保留源图并返回受阻，不移动原实体。真实状态、已执行动作和尚未绘制的正文分别留痕。

相同人物与场地的静态占位体复用，实时同步实体变换、碰撞层与形状；只在主体不再属于该资源/地图、实体转交或场地关闭时撤销，避免每次快照重建一批碰撞节点。

## 地图、呈现与资源

生活翼同层统一使用 `Maps/living_floor.tscn`，由 `tools/build_living_floor.py` 组合五张已发布场景生成；原场景和纹理保留为生成输入。四寝区隔离门是同图物理屏障，使用 DV 提供的各区放行值，不触发切图。逻辑房间标识保持 `zone_*:*` / `living_hall:*`，执行地图统一为 `living_floor`；室外分区及不同建筑不合并。公共厅中段补接六格以避免原局部底图相互重叠，门、标记、碰撞与灯光同步变换。

实际盲测可通过 `profile_dv_capture.py --controller player --window-mode offscreen` 启动真实 Main 场景，使用保存的渲染截图及 `input.json` 瞬时输入包操作普通视口输入。默认离屏且不可聚焦，保留图形渲染与物理，不使用 headless 逻辑运行替代；独立盲玩代理只读取该轮画面与通用输入协议。`--window-mode window` 可恢复可见窗口。

`dvg_snapshot.geometry_bindings` 提供地图站点、道具、纹理、位置与 footprint。`CampusMapView` 按声明建立 Marker2D、Sprite2D、碰撞与导航；相同声明不重复重建。场景和动态呈现均由内容声明驱动，宿主不以解析剧情动作 ID 替代空间绑定。

`CampusMapView.prepare_map(map, snapshot, tree)` 异步准备资源、几何与导航，返回 false 表示不可用或关闭取消，不表示移动结果。Main 在首次可见/离屏 setup 前传入自己的 SceneTree 并等待；跨图执行也等待准备后重新核对源实体、源图与真实入口。`FieldResources.gd` 通过线程加载请求准备资源，以有限缓存和批内强引用管理生命周期；Node 实例化及几何编辑仍在主线程。每个适配器按三个地图布局的上限淘汰空闲缓存，当前显示和在途准备的布局保留到交接完成。房间候选索引按约 2 ms 预算分帧准备；`prefetch_snapshot` 只在可见场地预备邻接地图，隐藏场地按正式行动需要准备，不改变当前地图、通行资格、主体归属或 DV 状态。实际实体、占位和可见淡入完成后才由 `finish_transition()` 释放切换状态。关闭取消等待并释放离树布局，已排队的 access 刷新拒绝关闭后清空的记录。

`ShareAsset/Internal/Props/Chapter2/manifest.json` 是运行时路由、出生点与尺寸输入。`CampusMapView` 根据建筑底图基名动态载入 `rooms/*.relief.tres`，这些 CanvasTexture 依赖相应 `*-normal.png`。动态路径与资源侧车都属于依赖检查范围，不能仅按场景中的字面引用删除资源。

场景基础格为 32×32，角色基础设计格为 32×48。正式人物优先使用 `Characters/PixelCast/<key>.tres`，缺失时按现有合同回退至 AnimeCast；对白使用 DialogueCast 立绘。共享家具和证物仍分布于 `FirstAwakeningInterior` 与 `AwakeningEvidence`，它们由现役统一地图消费。资源职责、来源与重建前提见 `ShareAsset/Internal/Characters/README-zh.md` 和 `ShareAsset/Internal/Props/Chapter2/README.md`。

运行时资产放在 `ShareAsset/`；若提供高分辨率源稿、切片工程等生产输入，放在受 `.gdignore` 保护的 `AssetsSource/`。当前 Git 树并未包含全部历史生产源和配方，重建前必须核对输入实际存在；缺源时保留现有运行资产并报告缺口，不把它们当可随时重生的缓存。生成文件从对应工具重建，不直接修改成品；调整路径同步核对场景、动态资源路径、插件配置及生成工具。删除资产时只移除该资产对应的导入侧车，保留其他 `.import` 的导入配置。

UI 样式、颜色与排版统一由 Theme/StyleBox 等 Resource 维护；不在节点中随意添加 `theme_override_*`，也不在脚本中临时覆盖主题或新造样式盒。非原生二进制素材的 Resource 使用文本 `.tres`。现役 `DialogueTheme.tres` 与 `TitleTheme.tres` 由 `tools/build_ui_theme.py` 生成；共有字体和纹理属于其依赖，不与未使用的主题分支一起删除。

标题桌面采用 `TitleUI.tscn` / `TitleUI.gd` 与 `DeskStage3D.gd` 的真实 3D 实现：桌面、书本、灯具及摆件使用真实几何，在同一 `Camera3D`、同一空间尺度与光照下渲染；火焰、灯光能量与受光材质必须使用一致的闪烁驱动。透视、遮挡、接触关系和投影由共同场景承担，不得用相互独立的屏幕贴片伪造不同视角的物体。`BookProjection3D.gd` 负责显示和输入的共同相机契约，将屏幕射线与书页平面的交点逆映射到 `580×820` 的页面 SubViewport，并把真实鼠标输入交给原生 Control；相机偏移、窗口缩放与视觉位置变化时必须保持命中一致。

`BookLeafTurn3D.gd` 及 `book_leaf_mesh_3d.gdshader` 提供具有独立正背面、真实厚度和边缘的页面网格；硬书皮使用 rigid-cover 行为，纸页允许曲面弯折，书脊位置、翻页方向与下层页面状态保持一致。旧页内容通过独立 SubViewport 的 `UPDATE_ONCE` 渲染后冻结，生产翻页路径不得逐帧执行 GPU 图像回读或重建页面纹理；截图工具为记录证据而读取图像不属于生产动画路径。`reduce_motion` 开启时直接落到目标页面并结束当前动画，恢复正常输入，同时停用视差及明显闪烁，不得留下半翻书页或输入锁。

标题材质按颜色与表面数据分别维护：当前 `reference-material-atlas.webp`、`reference-cloth.webp`、`reference-paper.webp` 为参考图辅助生成的颜色素材；`tools/build_desk_surface_maps.py` 使用 Blender CPU Cycles 从独立微表面节点烘焙 `reference-normal.png`、`reference-roughness.png` 及布面/纸面裁切图。法线使用 OpenGL 切线空间 +Y，粗糙度使用线性数据，二者不按颜色纹理转换。静态书体与翻页网格保留切线，法线框架随页面变形和背面 UV 同步；UI 字样不参与表面高度生成。`BookSurfaceDetail3D.gd` 提供静态书体倒角，`paper_mount.gdshader` 仅修饰纸签背景边缘。玻璃使用透明材质和场景反射，能力边界与资产来源写入对应 AssetsSource README。

桌面实物比例的唯一输入为 `AssetsSource/Internal/Props/UIDesk/Painterly3D/desk_physical_spec.json`：记录书封毫米基准、实物规格来源、各道具目标尺寸和布局。几何生成器按实际网格顶点标定，输出 `ShareAsset/Internal/Props/UIDesk/Painterly3D/desk-physical-layout.json` 的测量尺寸和灯芯顶点；`DeskStage3D.gd` 从该配置读取灯芯与火焰尺寸，不另写独立灯光位置常量。改变道具体量时同步核对落桌、整段翻页扫掠、屏幕遮挡及反射体积。相机俯角与取景距离独立维护，拉近镜头不得隐含降低俯角；按照设计规范指定的俯角保持近景，物体放大时不得拉远或扩大视野来强行容纳全体道具。构图通过实际摆放与自然裁切调整，开书只平移跟随阅读页。按钮射线激活成功不能替代画面遮挡检查。

`tools/build_painterly_desk.py` 使用 Blender 4.2 生成桌面几何；可编辑源稿保存到 `AssetsSource/Internal/Props/UIDesk/Painterly3D/painterly_desk.blend`，运行资源为 `ShareAsset/Internal/Props/UIDesk/Painterly3D/desk.glb` 与同目录 WebP 材质。源稿与运行资源分开管理，运行场景不依赖 Blender 在线执行。`tools/capture_title_3d.gd` 是当前 3D 标题界面的观察入口，必须使用有可见渲染窗口的 Godot .NET 构建，并预先完成 C# 编译；它仅实例化生产 TitleUI，通过真实输入检查页面与按钮，输出 PNG 和二进制 `ui-input.dvlog`，不加载 Main、不启动 DV 会话，不能据此宣称游戏或存档恢复已验收。`tools/capture_title_book.gd` 保留为旧 2D 入口，其结果不代表当前 3D 标题界面。上述为实现与验证契约，验收状态以实际运行证据为准。

以下既有载入与路径恢复约束保留原文，未因本次标题界面工作重新验收：

续读列出 user://saves/*.dvsave，也可显式选择文件，向 Main 发 load/path；GameEngine 在原串行队列用 DvSession.RestoreFile 恢复，拒绝尚待现场回执的存档。文件验证失败不安装会话，load_error 返回书页；不得据此声称跨版本与现场恢复已验收。

`UITransition.gd` 管理可取消的进入/退出状态，过渡使用现实时间，不受游戏播放倍率影响；`reduce_motion` 修改实时生效并使当前过渡落定。`ModalPanel.gd` 负责打开、退出期间的输入锁、焦点圈定/恢复和完成信号，实际退场后才发 closed。UISystem 串行切换设置、调查及其各面板，上层弹窗阻止底层对白或现场消费同一输入；现场选择在弹窗退场后发回原 site 与 choice_id。Title 的设置层高于标题层，Main 等待 `transition_out` 后更换视图。UISystem 提供可等待的 `transition_in/out` 与 `fade_in/out`，屏幕遮罩覆盖期间保持 busy；WorldView 只在目标准备完成后进行可见跨图淡变。

对白、回看、提示各自取消旧任务；空对白与立即完成的低动态效果也提供可等待的完成结果。换行保留正文顺序与来源，立绘通过线程请求预备后绑定，旧请求不覆盖新说话者。重入、关闭或新提示不得遗留能隐藏当前界面的旧回调。

`RoomStage` 负责当前房间构图、局部灯光与有上限的粒子；离屏执行不创建舞台特效。`reduce_motion` 关闭微尘及缩放动画。房间构图、着色器和灯光不改变碰撞、门洞、DV 目的地或人物认识。对白保留原透明立绘与滚动正文、原文顺序、说话者映射和用户继续操作，不能把界面播放完成当作行动成功。

Session 呈现保留 surface/lines 和 `source_refs`。Main 为发出的正文记录 `execution_event`、`source_id`、`source_field`、`emitted_line_index`、`paragraph_index` 与 `line_id`。过滤后的显示序号不冒充源行号，缺少来源不补造；`PlayerViewTrace` 在实际 `frame_post_draw` 后记录首帧，待显示正文与已绘制正文分开。

## 保存、计时与证据

`input_gate_state` 在实际绘制后的输入门状态变化时记录 UI 占用、阅读、提交锁、待执行主体/动作、现场任务、地图切换和队列长度。它只诊断“强制等待”，不修改准入、调度或输入锁；与实际输入、Prepare/Complete、现场执行和玩家截图配对解释。

显式保存/恢复拒绝现场 pending，异步维护先等待后台任务并再次核对。关闭允许保留真实 pending，等待在途工作后分别封存输入、可见正文、计时、会话及存档。每项输出独立捕获异常，一项失败仍尝试其余项；先写 `.partial`，成功才发布正式名，失败保留部分文件。Session 的写接口自行发布，宿主不重复改名；关闭链只调用一次 SessionClose。证据目录独占创建，不覆盖既有记录，异常也释放关闭等待。`TraceClosed` 只表示关闭结束，完整性依 `close-result.dvlog` 中各阶段结果判断。

新运行记录使用带校验的二进制格式：输入、可见正文、计时、采样、帧索引、关闭回执为 `.dvlog`，原始会话为 `.dvdag`，存档为 `.dvsave`；保留全部字段、精确数、顺序及共享不可变节点。JSON 只用于进程内消息、编译配置和不承载运行轨迹的小型工程元数据。旧 JSON 记录仅只读兼容，不能成为新轨迹格式；含逐段原始记录的分析输出同样写 `.dvlog`。

`GameEngine` 用 Stopwatch 单调微秒记录 interaction_id/command_id 关联的排队、Prepare、快照、现场执行、Complete、绘制首帧与阅读结束；局部行走单列真实起止和游戏钟。`PollTimings` 消费在线副本，不清掉封存用完整记录。缺段保留未知，CPU、阅读、等待和实测游戏时间分别解释。

阶段观察器按每个 Prepare/Complete 命令重新绑定，记录 thread_id；投影、JSON、Godot 物化及主线程交接分别计时。同一命令可有 intent、viewer、executor 多个物化段，分析应按 command_id、阶段及 details 配对，不以一个 interaction 内首次同名阶段合并多个动作或快照。

`RuntimeDiagnostics` 在实际 `frame_post_draw` 记录较长绘制间隔、位置变化与节点视口命中。视口命中不代表穿透遮挡可见或人物已经认出对象。脚本/GPU 与 .NET 栈分别封存；墙钟重叠不等于 CPU 归因，未知残差、未闭合阶段、采样缺口及分析器自身开销均如实记录。

`FrameArchive` 只接收主线程复制的 RGBA8 像素，后台串行完成无损 PNG 编码、哈希及原子写盘，不调用 Godot 场景/资源 API。PNG 成功后才追加帧索引，最多八个未完成图像任务；队列满则记录 backpressure 与采样缺口，不阻塞主线程或默默丢图。主线程读回/复制与后台排队/编码/写盘分别计时。后台同序追加已序列化记录，文本不能静默丢弃；关闭先记录真实游玩终点，再排空任务、完成通知及其追加的索引，封存尾部时间单列。

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

禁止执行根目录列明的流程回归，不能以断言数量、通过率或“全绿”评价游戏。构建、语法、生成物一致性仅用于工程排错。一级是无窗口正式 DV 逻辑输出；二级 UI 检查实际截图；三级只有明确要求最终效果时执行，默认由引擎侦探提供正式输入。独立盲玩需要用户明确指定，操作代理只看本轮允许的画面和输入资料，不读设计、源码或隐藏诊断，不预写路线和答案。具体时限与倍率按当轮约定，不沿用历史任务窗口。

完整行为结论依新正式会话的实际输入、可见结果和回执；图片、正文、原始事件、计时和来源分别保存，未发生/未覆盖/记录不足分别说明。不得放宽预算、正确条件、碰撞或计时规则制造提速，不把四倍播放、精简审计、纯几何夹具或短启动当作剧情通关和同条件性能改善。先完成当轮整批修改，再按要求统一排错和冻结；中途改源则结束该批次并保留失败证据。
