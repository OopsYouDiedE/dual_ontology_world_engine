# Godot 游戏宿主维护与路径规范

## Core 与现场接线（2026-10-19）

正式入口使用 `GameEngine.ConfigureSession` 的 `{controller: "player"|"ai", viewer?: string, campaign?: record}`，只允许新会话打开前配置。默认主体、内容参数与可视资源绑定在 DV `dvg_host_open` / `dvg_host_viewer`；宿主不另写人物标识或决策规则。玩家候选与 AI 规划最终进入同一 Prepare/Complete，控制方式不在在途动作中切换。

Main 的导出参数与 `--dv-controller=player|ai`、`--dv-viewer=`、`--dv-profile=`、`--dv-zone=`、`--dv-seed=` 配置同一入口；`--dv-auto-start` 仅触发正常 start，不选择动作。GameEngine 串行消费输入，后台完成 DV 状态转换和观察者/执行主体快照，主线程读取缓存。`dv_action.execution_snapshot` 仅供现场执行，不送入观察者正文。

Main → DvFieldExecutor → WorldView 使用唯一保留的人物实体执行移动。离屏场地使用独立 World2D 的同一地图、寻路和碰撞代码；切图保留真实实体和终点，初次物化核对声明地点与合法出生点。回执核对主体、序号、动作、地图、房间及显式站点到达误差。缺资源或绑定返回 blocked，不以逻辑执行器替代几何到达。唯一 Complete 在执行器返回后提交，普通现场信号不重复结算。

显式保存/恢复拒绝现场 pending；异步存取等待后台工作后再次核对。关闭等待后台工作并保留现场 pending；错误停止继续准备并记录到关闭证据。用户输入与显示记录先保存，原始会话和存档仍流式封存。工程构建与界面专项观察分别报告，不据此宣称完整剧情或五日达成。


本目录为 Godot 4.6.1 .NET 游戏宿主，负责场景树、渲染、空间与碰撞、物理与输入、UI 及音效呈现。通过 ProjectReference 单向引用 language_core/Session；Godot 类型、运行时专属状态和素材完全由本目录管理。设计与验收标准详见 [统一设计规范](../docs/design-standard.md)。

**同步修改规则**：本目录发生架构、模块合同、命令或排错方式调整时，必须同步修改本文件（`godot_csharp/AGENTS.md`）。

`addons/dv_checker` 提供 DV 编辑与检查底部面板，通过 `language_core/tools/check_dv.py --format json` 调用实际编译器。它支持仓库外部于 Godot 项目根目录的 DV 文件；诊断保留源文件位置，不执行剧情，也不改变当前游戏宿主的 Core 引用。插件维护方式见该目录 AGENTS.md 与 README.md。

`scenes/Host/DvRuntime.cs` 接入独立的 `language_core/Session` 生成类库，只负责 Godot 与 DV 会话的序列化边界。Open/Prepare/Complete/Save/Restore 使用同一份 DV 会话状态；现场须给出真实完成回执。Main/GameEngine 已调用 DvSession.OpenCampaign，正式项目移除旧 Core/GameSession 引用及旧检查工具编译入口。Prepare 在后台计算，EventsReady 把意图交回 Main；移动经 WorldView 的实际导航回执，显示只来自 DV 的本人行动/实际听闻。旧 AI pilot 接口尚未迁移，地图跨章和现场完整性待实机核对。构建通过 `-p:DvPython=<Python路径>` 指定生成器环境。

## 单一动作实现（2026-10-14）

本节覆盖本文及旧文档中的 r20、旧 Core、解释链和旧工具入口描述。用户要求清理整个项目的旧动作实现；旧源码只能从 Git 历史查阅，历史 artifacts、保护基线、`.story` 与译文保留，不重新编译成可运行旧路径。

唯一正式动作链是 `verification/session/30-runner.dv` 的 prepare → 执行回执 → complete：DV 负责主体决策、报价、权威效果、观察、传播与结算；Godot Main → WorldView 只执行移动并返回现场结果；非移动使用同一 complete 的 DV 世界法则。命令行 formal-model 是同一接口的逻辑执行适配，不含 Godot 几何，不能冒称实机验收。预测与推演不发布真实行动效果。

宿主只保留 GameEngine → DvSession → 生成 DV 类库，不保留旧坐席 token、DispatchField、舞台动作分派或第二个 DvRuntime 会话持有者。界面呈现完成不单独触发 Prepare，只有 view_ready / dv_next 启动下一步；等待回执时不重新分派意图。

构建：`dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers -p:NuGetAudit=false`；必要时用 `-p:DvPython=<Python路径>`。DV 编译器自举与编辑器继续使用现有工具，seed 只持有语法 Reader/Value，不含世界执行或解释器。两份解决方案均引用 Session。

旧 verify、FullTest、guarded_level3、PlayerTranscript、回放与旧会话检查入口已移除，不再执行。原保护清单和原始证据保持原字节；本次架构清理不是同条件耗时优化，不建立或重置测量基线。一级三份记录与三级保护运行工具需要以后按 DV 合同重新实现；本轮不以构建或短启动记录代替任何完整验收级别。

只读逐文件审计和清理报告继续放在 `artifacts/partial-bootstrap-core/single-action-path/`。禁止流程回归以及以计数评价游戏的约束继续有效。

## 测评方式约束

- 严格遵循根目录用户裁定：禁止执行流程回归（包括 `flow`、`flow2`、`flow_check.gd`、`check_flow.gd` 及统一回归调用）。
- 禁止以“xx 项通过”、断言数量、通过率或“全绿”评估游戏、人物或修改效果；所有机制检查仅用于定位具体工程问题，不构成验收依据。
- 验收必须使用新的正式游戏会话，逐段核对玩家实际可见信息、真实输入与执行回执、画面表现及推进；涉及停滞、折返或画面问题时，必须说明当时信息并查看对应截图。
- 三级测试默认由引擎自身的侦探替代玩家输入（使用主体认知、动机与共同决策机制在正式游戏循环中执行），测试 Agent 负责独立审查；只有用户明确要求盲玩时才采用独立盲玩。
- 关键节点耗时由只读 `*.Measurement.cs` 通道复制正式会话已提交的进度，并由 FullTest 写入独立诊断流；不创建缺失进度组，不调用推理、判题、观察或输入，不把诊断提供给主体。变化时刻以相邻采样区间表示，玩家画面与执行回执另行核对。此通道、正式输入、计时、剧本和判定均受根目录的保护基线约束。

## 宿主与核心合同

1. **分层隔离原则**：
   - UI 控件严禁直接访问 Core 对象引用；Core 严禁依赖任何 Godot 场景或节点。
   - `Host/Main.tscn` 与 `Host/GameEngine.cs` 是唯一胶水，负责双向数据转发：将 Core 演算结果与回执转为纯数据派发给 UI 和现场，将玩家/坐席操作转为 Core 意图交由世界引擎裁决。

2. **主体共同决策与适配**：
   - 当前生产源码按用户要求恢复 `5b8c7dbe` 的 r20 时期提交；使用该版 Core 主体决策与宿主坐席适配，未提交的新版 `SubjectiveAgent`/Diagnostics 合同封存后移出。设计合规需另行核对，不因旧版推进较快而推定合格。
   - 宿主（`PlayerController`、`AgentPilot`、`ActionLearning`）仅负责将界面/现场当前可见的选项与操作转换为语义候选，并将真实执行阶段（开始、接触、完成、失败、无进展、中断）反馈给 Core，回执保持幂等。
   - 宿主严禁按席位另设行为评分或主观偏好，角色差异完全由 `.story` 声明的个人动机、已知信息与能力预算决定。

3. **保存与状态恢复**：
   - `WorldSession.Save` 与 `SessionPersistence` 提供空闲会话的原子持久化协议，仅记录差异片段。
   - 章节恢复使用专用构造加载，不重新触发观察、效果或初始化。非空闲点与未支持状态应明确拒绝保存。

4. **现场表现与空间导航**：
   - 现场表现（演员节点、资产、地图、站位、热点与出口）严格由剧本编译物声明驱动。
   - 人物移动受正式物理、碰撞与视线遮挡约束；导航仅基于本人已见区域规划，不预读未知几何或直接设置坐标。

5. **AI 坐席与全量复测**：
   - AI 坐席（`AgentPilot.gd` / `GameEngine.ConfigureAiSeat`）驱动玩家席位，基于本人认知与声明规则，通过正式现场与 UI 动作进行游戏。
   - 实际复测使用 `full_test.py`，完整记录真实决策、输入、回执与渲染截图。评估以实际行为合理性为准，不采用计数汇总。

## 一、场景（.tscn）架构分类

项目中所有场景及预制件必须明确归属于以下三类之一：

1. **小组件（Components / Widgets，`UI/Components/`）**：
   - 最小可复用单元，不管理全局流程生命周期，受父级容器控制或纯数据驱动渲染（如按钮、标签、模态窗骨架、证物卡片、对白选项等）。
   - 保持高内聚，对外仅暴露必要的属性与交互信号，不持有业务全局状态。

2. **UI 面板与子系统（UI Views & Subsystems，`UI/Panels/<面板>/`）**：
   - 完整的界面功能页、浮层或视觉交互系统（如对白面板、菜单、卡片、表单、笔记本、地图、HUD 等）。
   - 通过标准化信号向外发送用户意图，接收结构化数据刷新展示，与底层逻辑实体解耦。

3. **宿主承接与信号总线（Main / Host，`Host/`）**：
   - 游戏宿主顶层胶水（`Main.tscn` / `GameEngine.cs`），唯一直接承接 C# 逻辑会话并驱动游戏循环的节点。

### 场景目录映射

| 分类 | 目录 | 内容 |
| :--- | :--- | :--- |
| 小组件 | `UI/Components/` | `Paper.gd`（组件工厂）、按钮、标签、弹窗、印戳、手账页等复用控件 |
| UI 面板 | `UI/Panels/<面板>/` | `Dialogue`、`Menu`、`Card`、`Form`、`Notebook`、`Ending`、`HUD`、`Field` 等 |
| UI 总装 | `UI/UISystem.tscn` | 整合各面板，纯数据驱动，通过 `ui_event` 回传操作 |
| 宿主 | `Host/` | `Main.tscn`（主场景）、`GameEngine.cs`、`AgentPilot.gd` |
| 现场视图 | `Shared/Field/` 及各章目录 | 地图适配器、现场精灵、碰撞与出口表现 |

## 二、资源类型与样式规范

1. **UI Style 统一规范**：
   - 界面样式、调色板与排版统一使用 `Style` 类 Resource（如 `Theme` / `StyleBox`）。
   - 严禁在 `.tscn` 节点中随意写 `theme_override_*`，脚本中不调用 `add_theme_*_override` 或直接 `new` 样式盒；通过 `theme_type_variation` 或主题自定义条目读取样式。
2. **Resource 格式约束（.tres）**：
   - 除原生二进制素材（图片、音频、字体、模型等）外，Resource 统一保存为文本格式 `.tres`，禁止使用二进制 `.res`。

## 三、素材与资产管理

1. **生产源与运行时资产物理隔离**：
   - `AssetsSource/`：生产源仓库（配置 `.gdignore`），存放高分辨率原画、PSD、切片工程等。
   - `ShareAsset/`：运行时正式资产库，游戏引擎在运行时唯一合法引用的素材目录。
2. **资产族（Asset Families）归类**：
   - `External/<Package>/`：外部引入资产包。
   - `Internal/Characters/<Family>/`：角色资产（像素帧、半身立绘、动作图集）。
   - `Internal/Identity/`：身份物、徽记、证物小图标。
   - `Internal/Props/<Family>/`：家具、道具、场景瓦片。
3. **规格标准**：
   - 地图基础格为 **32×32 像素**，角色基础设计为 **32×48 像素**。

## 四、常用命令与工作流

- **构建项目**：`dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers`
- **编译剧本**：`python godot_csharp/tools/compile_story.py`（检查一致性加 `--check`）
- **重新导入素材**：`& $env:GODOT --headless --editor --path godot_csharp --import`
- **实际行为复测（FullTest）**：`python godot_csharp/tools/full_test.py --name <运行名>`
- **一级验收 UI 导出**：
  ```powershell
  & $env:GODOT --path godot_csharp --script res://tools/capture_player_transcript.gd
  python godot_csharp/tools/export_ui_transcript.py artifacts/basic-ui-system/player-traces/ui-full-output.jsonl
  ```
- **二级 UI 选段截图**：
  ```powershell
  & $env:GODOT --path godot_csharp --script res://tools/capture_story_ui_samples.gd -- --round=<轮次名>
  ```
- **盲玩实时录像**（仅在用户明确要求盲玩时使用）：
  ```powershell
  python godot_csharp/tools/record_blind_session.py run --session=<fresh-id> --agent=<name>
  ```

## 五、工程排错工具

以下检查项目与脚本仅用于定位特定工程问题或底层序列化异常，**严禁作为游戏通关、人物行为或体验改善的评价依据**：

- `godot --headless --path godot_csharp --script res://scenes/Shared/check/check_session_save.gd`：保存与恢复底层检查。
- `dotnet run --project godot_csharp/scenes/Shared/check/SaveCheck/SaveCheck.csproj -c Release -- write <tmp_dir>`：纯 .NET 保存机制排错。
- `dotnet run --project godot_csharp/scenes/Shared/check/PlayerControllerCheck/PlayerControllerCheck.csproj -c Release`：玩家输入适配器边界排错。
- 新版 `UnifiedHostCheck` 与新增合同检查已随新版源码封存，不用于此恢复基线。

## 六、严禁硬编码剧情

严禁将剧情内容、对白文案、角色台词或谜题答案写入引擎代码（无论是 C# 还是 GDScript）。所有具体剧情与设定查验必须通过剧本编译物（`.story` 产物）和本地化词汇表读取。

2026-10-14 的正式切换覆盖上文旧 r20 生产架构描述。旧文件仅为迁移来源；不得调用旧坐席适配冒充 DV 会话。新 ReadMilestoneProgressJson 只读 DV 快照，历史测量工具字段尚需迁移，旧基线不能与新实现作同条件性能比较。

第二章 CampusMapView 从 DV 快照读取呈现绑定，不再读取旧第二章剧情 JSON。地图通路由场景出口与室外核心区域构建，实际寻路和出口触发后才返回完成；无在场身体或无法跨图执行的 NPC 返回未执行，不能据此改变 DV 地点。该离屏执行缺口仍待补齐。

单一现场入口为 WorldView.execute_move：地图适配器提供目的地执行，否则使用同一站点导航。旧 stage/直接键鼠移动/快照进出场和让路移动均已移除。快照只创建视图身体，不发起未准备的行走。短启动工程排错使用 `--script res://tools/dv_host_startup_probe.gd -- --out=<新绝对证据目录>`，八秒后封存截图与快照，拒绝覆盖旧目录；不得称为完整游戏验收。

三级无桌面输入采集：tools/dv_level3_capture.gd 在屏幕外、不取焦点的正式 Main 渲染实例中设置 Engine.time_scale=4；使用 -- --out=<新绝对目录> --mode=blind|detective --seconds=<现实秒>。input.json 仅接受 key、click、stop，通过 Input.parse_input_event 进入正常输入；detective 模式仅自动开始和继续对白，不选择世界动作。截图可提供盲玩者，measurement.jsonl/end.json 只供主审查，不得向盲玩者提供。该工具不补齐尚未接入的人工动作入口，不将默认自主侦探视为盲玩者决策，不执行流程回归。原始证据只新增，关闭时保留宿主原始输入与可见记录。四倍只加速 Godot 计时、物理与呈现，不改变 DV 行动费用或推演预算；DV 游戏钟取实测快照，不按倍速换算。
侦探组自动继续对白至少间隔1.5现实秒，首次出现也先等待，保留正文实际绘制机会；初版过快跳读的采集只作工程试运行，单独保留，不与修订版混为同一计时规则。

2026-10-14续测：正式UI默认人工控制，GameEngine.SetPlayerControlled仅配置DV席位，不选择行动；DV awaiting_input 提供可执行候选，UI选择回传同一Prepare，现场仍经WorldView和Complete。dv_level3_capture每200现实毫秒采集实际已绘制帧，frames.jsonl记录真实时刻；--seconds=0保持运行直到外部stop，四倍游戏时速不变。encode_dv_capture.py <目录>按原采集时间生成5fps recording.mp4，真实间隔不匀时明确保留末帧，不重跑游戏也不改游戏时间。
正式对白沿DV呈现返回的actor与speaker_name标注说话者；宿主不填默认人物姓名。
DV呈现中的显式换行逐段交给原对白继续流程，保留原文本顺序与输入确认，避免多段证物挤在同一矮框中。
采集另记录input-events.jsonl的协议实际消费时刻与host-events.jsonl的宿主交付时刻；后者为私有审查数据，不向盲玩者提供。prepare交付只证明已准备，不冒充执行、推演正确、答案接受或交接时刻；真实节点仍与正式事件及可见画面核对，无法精确定位时保留采样区间/记录不足。
界面仅依据DV返回的实际答辩事件显示接受/未接受，提交本身不作接受，也不作章节交接。

移动 destination 的 room 为逻辑呈现目标，execution_room 可声明当前地图的实际到达房间，departure_station 为站点；三者不可混用。地图适配器支持 site: 声明的现有热点站点，沿用热点既有可达范围。WorldView 同地点移动仍走原寻路/碰撞；现场事件保留站点、接近目标、实际终点、范围和原到达容差。显式 travel.station 的行动在 Main 核对同一主体/序号/行动/站点/房间及实际到达误差后才提交成功。没有手部动画或独立接触碰撞时，不将站点到达证据描述为手部动画验收。

2026-10-15：批量初始化可使用独占采用表构建器，先复制继承表，每条声明仍立即执行原验证顺序，再仅追加独立条目；finish核对旧前缀与长度，修订号按成功追加数增长，一次持久更新世界，空批保持原世界。中间采用表不得发布、分叉或当作任意更新入口，world/mind/预测仍保持原独立复制合同。
证据目标可显式声明refresh_until命题；仅本人尚不知该命题为真时，在目标组合时把minimum_time刷新为本人mind.time。此操作不改旧观察或认知，不授权新的传输频道，也不代选检视；本人时刻未推进时不承诺刷新有效。每个目标可独立停止刷新，原预算与判答不变。
Session新增流式WriteTrace/WriteSave，Trace的精确数仍为原字符串，Save仍为$dv_rational对象标签。宿主关闭等待在途工作、先保存输入与可见记录，再后台流式写原始会话及存档；先写.partial，成功才改为正式文件名，失败保留部分文件。SessionClose在该关闭路径仅调用一次，不自动重试。close-result.json明确各阶段错误与complete，TraceClosed仅表示关闭结束，不能代替证据完整性判断。新建证据目录不得覆盖既有目录；异常也必须释放关闭等待。必要工程检查对照中性值的序列化、别名、验证首错与目标新鲜度，不作游戏评价。
采集可在原5Hz截图之后追加私有render-state.jsonl，只读现有actor/AnimatedSprite2D可见性、位置、动画帧与当前纹理引用，并关联原real_ms和文件名；不加载资源、触发动作或更改呈现。失效节点、空纹理、无效动画帧与诊断文件打开失败容错。该记录不给盲玩者；额外磁盘开销随新来源封存，实际采样间隙照实保留，不能以隐藏节点状态代选行动或代替PNG实视。

## DV 到期意图合同（2026-10-16）

Prepare 返回已经到达逻辑完成时刻的单个意图，其他主体的排队行动由 DV 保存。宿主继续实际移动并回报现场结果，Complete 才发布效果。execution_cost 是该行动完整费用，已等待的游戏时长由 DV 去重结算；宿主不另推游戏时钟。尚未完成的现场执行继续使用同一 pending，不重新 Prepare。本轮仅进行无窗口逻辑检验，几何与呈现尚未验收。

## 可见正文的执行来源（2026-10-19）

Session presentation 保留原有surface/lines，并透传动作source_refs。Main为实际发出的正文标注execution_event、source_id（动作ID）、source_field、emitted_line_index与paragraph_index，line_id由真实执行事件和输出字段/序号组成。该序号是过滤后的显示序号，不冒称源文件行号或原声明索引。source_refs保留原声明提供的路径/行/ID，不补造缺少的来源。ChapterDialogue把这些只读元数据保留到已绘制控件与PlayerViewTrace，既不变更正文，也不写入人物认识。旧证据不补写编号。

## 原生空间声明与局部几何生成（2026-10-19）

`dvg_snapshot.geometry_bindings`按地图键提供sites与props的纯几何数据；CampusMapView创建Marker2D、Sprite2D及声明footprint对应的真实碰撞，并重建导航栅格。声明只在内容DV中保存，宿主不绑定具体剧情ID；同声明重复刷新不重建节点，切图重新应用目的图的声明。出生锚点与最终路径使用同一实际图中的节点。几何外观不授予读数、调查结论或采信。

`python godot_csharp/tools/build_chapter2_scenes.py --geometry-only <interior-key...>`从rooms配方和既有PNG重新生成目标室内场景与manifest，不重画图片、不启动Godot。geometry_sites只存ID与坐标，显示标题由native snapshot.sites提供；text-only/sites-only保留这些无文本marker。几何生成日志与图片指纹检查只用于工程来源核对，不能代替实际到达和画面验收。

初次物化与导航分开：native snapshot/people.initial_anchor是主体的明确初次锚点；地点anchor保留作移动目的地，不被所有同房人物共同当作出生点。缺初次锚点时只选既有同房且可走、未占用的Stand/Spawn；显式锚点也核占位。已有实体沿用真实位置。室外entered_room以地图实际边界及可走栅格判定，不读取不存在的室内terrain_rows；门区、跨图和最终物理回执仍独立核验。

## 集中修复与四倍播放（2026-10-19）

按用户最新要求，先集中修订本批已发现的源与几何缺口，再冻结、统一工程排错与四倍播放检查；中途发现需改源则结束该批次，保留原失败证据。本轮不以局部几何夹具或按钮工程触发宣称正式三级验收。

Main.playback_speed默认4，`--dv-speed=`可在0.25至8间设置，打开会话前应用Engine.time_scale；physics_ticks_per_second按60×max(1,speed)设置，四倍时240以维持单步物理间隔。输入记录host_configuration中的倍率与帧频；不把倍率写成DV费用、分钟或推演预算，不按倍率推算未发生的剧情节点。采集脚本需要现实秒窗口时使用ignore_time_scale计时，原始会话与纯几何夹具分列。

原生矢量道具通过`python godot_csharp/tools/publish_geometry_assets.py Internal/Props/Outdoor`从AssetsSource原字节发布至ShareAsset；AssetsSource受.gdignore保护，生产纹理只引用正式发布目录。

室内家具位置改变后使用`--interiors-only <interior-key...>`重生所选场景、manifest和底图烘焙阴影，避免旧阴影残留；该入口不重建室外。仅新增已有贴图而不改变原阴影时才使用geometry-only。两者都从源码生成，不手改tscn。

站点接近保持原半径和房间约束，除既有连续方向样本外枚举半径内全部可走格中心，避免窄合法接近点被有限方向漏掉。Campus.walkable同时核对格中心合法性与查询连续脚点是否落入原矩形障碍的膨胀范围；不删除碰撞或扩大站点半径。实际到达仍由CharacterBody2D移动、终点、房间和到达误差回执核验。
