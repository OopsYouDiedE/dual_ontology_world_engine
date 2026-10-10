# Godot 游戏层维护与路径规范

本目录为 Godot 4.6.1 .NET 游戏宿主，负责场景树、渲染、房内空间与碰撞、实时循环、UI 和游戏内容。通过现有 ProjectReference 单向引用 Core；Godot 类型、游戏专属状态和素材完全由游戏层管理。场景沿用现有对象绑定，保持简洁直接的架构。制作验收统一遵循 [设计规范](../docs/design-standard.md)。

**架构与同步规则**：本目录发生架构、路径或管线规范调整时，必须同步修改本文件（`godot_csharp/AGENTS.md`）；严禁将剧情写入宿主代码，剧情仅允许存在于 `.story` 和译文中。C# 与 GDScript 中的设定与逻辑查验统一通过剧本编译物与词汇表，不得硬编码剧情台词或设定。

---

## 一、场景（`.tscn`）架构分类

项目中所有场景及预制件必须明确归属于以下三类之一：

1. **小组件（Components / Widgets）**
   - **职责**：最小可复用单元，本身不管理全局流程生命周期，受父级容器控制或纯数据驱动渲染。
   - **范畴**：
     - 通用交互控件（如按钮、标签、模态窗骨架、分割线）。
     - 业务复用单元（如证物卡片、对白选项条目、心声气泡、手稿条目）。
     - 地图与环境预制件（如独立家具、门、机关物件预制件）。
   - **设计原则**：保持高内聚，对外仅暴露必要的属性与交互信号，不持有业务全局状态。

2. **UI 面板与子系统（UI Views & Subsystems）**
   - **职责**：完整的界面功能页、浮层或视觉交互系统，负责具体的排版布局、视觉动效及用户输入收集。
   - **范畴**：
     - 叙事交互层（如对话系统面板、分支选项层）。
     - 现场 HUD 与环境遮罩（如顶部状态条、线索提示层、视野遮罩）。
     - 复杂业务页（如笔记本、重演手稿、学园地图、答辩表单）。
     - UI 总装界面（如整合上述面板的总装 UI 树）。
   - **设计原则**：通过标准化信号向外发送用户操作意图，接收结构化数据刷新展示，与底层逻辑实体解耦。

3. **宿主承接与信号总线（Main / Host）**
   - **职责**：游戏宿主顶层胶水（如 `Main.tscn` / 章节 Host），**唯一**直接承接 Core 类库逻辑会话、驱动游戏循环的节点。
   - **双向信号转发**：
     - **下行（Core → UI/现场）**：消费 Core 演算产生的状态变更、对白事件、推演结果与行动回执，转换为纯数据派发给对应 UI 面板或地图现场进行呈现。
     - **上行（UI/现场 → Core）**：监听玩家在 UI 与地图上的操作信号，转换为 Core 意图（`ActionIntent` / 决策请求）交由世界引擎权威裁决。
   - **隔离铁律**：UI 控件严禁直接访问 Core 对象引用；Core 严禁依赖任何 Godot 场景节点。

**目录对应（`scenes/`）**：

| 分类 | 目录 | 内容 |
| :--- | :--- | :--- |
| 小组件 | `UI/Components/` | `Paper.gd`（组件工厂，只按类型变体创建、读主题色）、`PaperButton`、`PaperLabel`、`PaperRule`、`PaperModal`、`DialogueResponse`、`ReplayBubble` |
| UI 面板与子系统 | `UI/Panels/<面板>/` | `Dialogue`、`Menu`、`Card`、`Form`、`Notebook`、`Manuscript`、`CampusMap`、`Ending`、`HUD`、`Field`（提示层、回放层、视野遮罩）、`Title` |
| UI 总装 | `UI/UISystem.tscn` | 整合各面板；只读宿主事件与快照，用 `ui_event` 回传 |
| 宿主 | `Host/Main.tscn`（主场景）、`Host/GameEngine.cs`、`Host/PlayerViewTrace.gd` | 唯一承接 C# 会话的节点 |
| 样式 | `UI/Styles/*.tres` | `UITheme.tres`（纸页主题：语义色 `Paper/colors/*`、全部类型变体、自绘控件的颜色与尺寸）、`DialogueTheme.tres`、`TitleTheme.tres`，以及开发场景的 `MapBrowserTheme.tres`、`WorldScriptPreviewTheme.tres` |
| 编辑器总览 | `UI/Workbench/` | 各面板的预览页 |

现场（地图视图）在 `Shared/Field/`，各章地图适配器在各章目录；现场用到的物件外观名等文字由 `Shared/Field/FieldVocabulary.gd` 从剧本编译物读取。
普通家具悬停复用地图适配器的 `furniture_hits` 与现场词汇表，只对已绘制且在视线内的精灵显示外观名；剧情热点保持优先。

`stage_action` 从开始走位到动作结束持续占用人物，异常或取消时也归还；侦探被演出占用时快照关闭 `player_control`，现场输入循环不得覆盖演出姿态。

第一章流程中的 `stage_actor:<who>:<anchor>` 等待走位回执，锚点支持 `meeting:`、`post:`、`site:` 前缀；`stage_action:<who>:<anchor>:<anim>:<facing>[:<seconds>[:<after>]]` 到位后播放动作并等待指定秒数，再保持后续姿态（默认 0.8 秒、idle），走位失败则跳过动作。`post:<who>` 非阻塞启动单个同伴的岗位走位，`posts` 保留全体岗位走位；这两种命令用 `Start(..., immediate: true)` 先输出首个走位事件再输出后续对白，避免界面等待对白回执而延迟起步。流程负责安排这些指令与场面的先后。

第一章阶段的进入规则可以引用流程；会话按实际房间进入执行，重复演出的条件由流程自行管理。人物对话中的协助邀约由会话并行调度走位，流程参数保留当前交谈对象，走位期间不占用侦探控制。

流程参数来源在每次继续时重算并覆盖携带的派生值；交谈对象作为显式输入保留。协助邀约使用可达的物件邻近点，完成后复用增量证物卡。地图连续落点与导航网格共用角色脚印的通行判定。`StoryPacketCheck` 覆盖携带旧参数时的重新提问、重新裁决与步数推进。

第一章阶段的 `时刻 <分钟>：流程 <名>` 编译为有序 `times`；会话在结算共享时钟与远处生态后、现场空闲时执行到期流程，按阶段与时刻记入进度，跨过边界和阶段再进入均不重复执行。`clock.door_open_minutes` 只记录首次开门，续章用该值与其起点取较大值。

`enter:<人>:<地点>` 通过 `stage.spawn` 与现场回执登记来访演员；名称、立绘、交互热点和 `enter_<人>_<房间键>` 锚点来自剧本。来访演员加入现场与交谈注册，现实位置由几何同步并暂停原远处日常；不加入 `Person`、四人案件、回忆、重演或同伴协助集合。行走帧复用共享角色资产。第一章地图保持原有通行边界，门洞邻近提示与开门显示由剧本数据和快照驱动。

生态编译为晨间、出区日常补入其引用的续篇地点声明，供低精度推演使用；不因此增加第一章现场地图或继承续篇场面。

第一章普通交谈、协助场面未单独声明时长时，使用对应行动时长，避免再套用默认剧情场面时长；显式声明的剧情场面仍按编译数据结算。

第二章的时段流程和嵌套选择由 `.story` 编译到 `design.flows`、`design.schedule`；`Chapter2SessionFlows.cs` 执行步骤并等待正式对白或菜单回执。场面与耗时动作由流程组织，会话不得另列一套时段场面表。`Chapter2Access.cs` 统一管理访问权限，在现场快照、实际通路和 NPC 内部通路投影中同步；权限变更与即时调停写入有序 `FreeEvents`，夜间快照保留权限集合。人物菜单通过 Core `SocialSystem` 的只读预览排序，实际收到的请求记录相合分供自主决策复用，拒绝在时段结算之前处理。

`scenes/Main2D.tscn` 是只装载第一章现场的旧预览场景，不承接完整游戏会话。`scenes/Shared/UI/*.tscn` 保留旧界面场景文件，归于 UI 面板类；正式界面仍使用 `scenes/UI/`。这些旧文件不作为正式运行或验收入口，启用前必须先检查外部资源引用是否存在并完成实机验证。

---

## 二、资源类型与样式规范

1. **UI Style 统一规范**：
   - 所有 UI 界面的外观、调色板、边框与排版样式，**统一使用 `Style` 类型的 Resource（如继承或封装自 `StyleBox` / `Theme`）进行管理**。
   - 避免在各 `.tscn` 节点内部随意硬编码行内样式覆盖；公共视觉规范统一定义在主题与样式资源中。
   - 场景节点不写 `theme_override_*`，脚本不调用 `add_theme_*_override`、不 `new` 样式盒；节点用 `theme_type_variation` 引用主题里的类型变体，自绘控件用 `get_theme_color` / `get_theme_constant` / `get_theme_font_size` 读取主题里自定义类型的条目。
   - 编辑器预览用的样例文字只写中性占位，不写剧情与设定。

2. **Resource 格式约束（.tres）**：
   - **除原生二进制素材（图片 `.png`/`.webp`、音频 `.wav`/`.ogg`、三维模型 `.glb`/`.gltf` 等）外，项目中绝大多数 Resource 统一保存为文本格式的 `.tres`**。
   - 禁止随意使用二进制 `.res` 存储逻辑配置或样式资源，确保版本控制的可读性与 diff 可追溯性。

---

## 三、素材与资产管理（动静分离与资产族）

为保证资产管理清晰、加载高效且避免污染，素材实行物理隔离与按族自治：

1. **生产源与运行时资产物理隔离**：
   - **`AssetsSource/`（生产源仓库）**：
     - 根目录配置 `.gdignore`，完全隔离 Godot 引擎扫描与 `.import` 生成。
     - 仅存放高分辨率原画、PSD、切片工程配置及提示词说明（`prompts.md`）。
   - **`ShareAsset/`（运行时正式资产库）**：
     - 游戏引擎在运行时**唯一合法**引用的素材根目录。
     - 仅存放切片整理完成的运行时纹理、音频、字体以及关联生成的 `.tres` 资源。
   - **`imported_assets/`（转换流水线临时输出）**：
     - 受 `.gitignore` 保护的本地试验输出，经管线检验合格后才规范发布至 `ShareAsset/`。

2. **资产族（Asset Families）归类目录**：
   `AssetsSource/` 与 `ShareAsset/` 内部保持统一的结构对齐：
   - **`External/<SourcePackage>/`**：外部购入或开源资产包，保持原始包名、结构与许可声明完整。
   - **`Internal/Characters/<CastFamily>/`**：角色资产族（像素帧、半身立绘、动作图集）。
   - **`Internal/Identity/`**：身份物、徽记、证物小图标。
   - **`Internal/Props/<SetFamily>/`**：内部家具、道具、装饰图集与场景瓦片。

3. **规格与管线纪律**：
   - 地图基础格为 **32×32 像素**，角色基础设计为 **32×48 像素**（64×64 像素为透明承载格）。
   - 外部素材严格依照原生规格读取并完成切片适配，不可破坏原生网格。
   - 每个资产族在自身目录下附带 README 说明其切片规格、源路径与使用场景。
   - `Chapter1Rewards` 的原生像素配方位于 `AssetsSource/Internal/Props/Chapter1Rewards/build.py`，逐字节发布到 ShareAsset 同族；重建后运行 `tools/build_awakening_map.py` 统一更新精灵、碰撞和导航足迹，再导入 Godot。

---

## 四、工具与插件管理

- 自动检查与固定路线工具的情境、路线和封存预期存于 `scenes/Shared/check/Fixtures/`，以稳定选项、行号、证物或夹具键查询；读取器 `FixtureData.cs` 仅由检查/工具项目链接，`fixture_data.gd` 仅由自动检查加载。黄金预期不从被测算法或正式真值表在运行时生成。第二章 `Prototype/Bots.cs`、`Checks.cs` 只由 `Prototype.csproj` 编译，正式 `Chapter2Script.csproj` 排除这两份开发代码。
- `PlayerTranscript` 的调查、相遇/答辩输入及连续日程由 `Fixtures/player-transcript.json` 的路线步骤提供，工具只分派输入、等待与记录，保留两章结束断言；正式输出的房间到场回执仍由空间适配器处理。可在仓库路径后传 `--output <目录>`，把本轮采集放入隔离目录；省略时沿用原记录目录。隔离采集与封存预期各自保存，复测不得覆盖旧证据。

- 源码内容审计使用根目录的 `STORY-HARDCODE` 标记约定，分别记录正式代码、夹具、工具、示例和编辑器预览。剧本生成的 C#/JSON 只登记来源，不手改；专用绑定与剧情因果分别分类。仅加标记时验证原有源码不变、宿主构建与修改过的 GDScript 语法，不将其当作一级、二级或三级验收。

- 第一章 Godot 自动检查统一通过 `Awakening/check/chapter1_fixture.gd` 加载 `Host/Main.tscn`，发送标题页 `start` 并等待正式视图握手；需要略过开场时实际推进对白，不使用 `skip_intro` 元数据。宿主或视图加载失败立即退出非零状态。`Shared/check/ChapterProbe.cs` 仅供检查，反射访问统一会话持有的 `IChapterProbe` 并复用宿主 Variant 转换；正式输入接口不开放状态注入。自动检查结论不代替任何一级、二级或三级验收。
- 第二章 `Chapter2/check/check_flow.gd` 通过 `chapter2_fixture.gd` 复用上述 Main 夹具，先用调试跳转推进第一章终局，再以章末卡 C 键触发连续会话的视图切换与 `view_ready`，检查首个可操作状态。后续输入和 UI 回执仍经过 `Main.dispatch`，不再加载旧章节宿主、调用引擎直通接口或单独注入继承快照；地图加载失败和流程等待超时须报告并退出非零状态。
- 社交请求的流程 AST 使用发言者内部世界的引用提交；接收者认知只用于筛选已知目标，发言传播再将请求映射到接收者内部世界。第二章流程回归按正式日程检查开场与首课，检视物证后再验证同行结算。
- `tools/`：素材批量切片、图集生产、场景组装及 Godot 命令行测试工具。
- 地图文字更新先在仓库根目录运行 `python godot_csharp/tools/compile_story.py`。`python godot_csharp/tools/build_awakening_map.py --text-only` 从编译 layout 的 `furniture_asides` 与 Content 的 `ROOM_NAMES` 按固定槽键更新既有 `map_layout.tres`，完整校验槽数、房间集合与文字类型，不绘图、不写场景几何。
- `python godot_csharp/tools/build_chapter2_scenes.py --text-only <室内地图键>...` 从正式 Chapter2 `design.text` 更新既有根节点标题、`Sites/<id>` 标签与 manifest 对应文字。支持 `living_hall`、`zone_jia`、`zone_yi`、`zone_bing`、`zone_ding`、`central_hall`、`lecture`、`archive`、`counterweight`、`resonance`、`clinic`、`logistics`、`granary`、`greenhouse`；整批校验键、节点和场点集合后才写入指定场景与 manifest，仅替换字符串，保持几何、资产、节点顺序与其他序列化字节，文字未变时不写文件。
- `python godot_csharp/tools/build_chapter2_scenes.py --sites-only <室内地图键>...` 的几何来自 `chapter2_art/rooms.py`，文字来自正式编译物的 `design.text`；更新指定地图的 `Sites` 标记与 manifest，复用既有纹理。该入口仍将 `Sites` 起始处至文件尾重写，仅调整热点时不必重新生产整套美术；标题更新使用 `--text-only`。
- 两章全量生成同样从正式编译物查文字；`tools/map_text.py` 负责拒绝 JSON 重复键与缺键、类型错误，并保留纯文字更新的原序列化。资源文字自检为 `python godot_csharp/tools/check/check_map_text.py`，使用隔离夹具核验整批拒绝、重复执行与文字/几何独立性；它不代替游戏验收。全量资产重生与 Godot import 继续沿既有管线执行。
- `tools/verify_ch1_locker_ui.gd` 经正式 Main 夹具推进开场，采集附加家具的动作、悬停和点击画面，核验共用导航、实体碰撞、演出期间玩家控制权、锁定及视野外悬停抑制和热点标题优先。动作图保留起始帧并等待自然播放到伸手帧另存，不直接设帧；家具指针选真实不透明且未被可见热点占用的像素，保留日常人物活动。Main 从创建时就在独立渲染视口内，复用标题 `start` / `view_ready` 握手，鼠标事件传入同一视口；需要有窗口的渲染器。运行时传 `-- --round=<独立轮次>`，证据保存在 `artifacts/ch1-locker/<轮次>/`；`--pointer-only` 仅用于采集器调试，不能作为完整验收。完整轮次属于二级 UI 专项与开发检查，不作为盲玩记录。
- `addons/<plugin_name>/`：编辑器插件（如素材适配器 `rpg_asset_adapter`），代码与资源保持插件内部封闭。
- 资产迁移或管线变动后，通过 `& $env:GODOT --headless --editor --path godot_csharp --import` 完成重新导入校验。
- 一级验收的 `capture_player_transcript.gd` 以正式 UI 实际绘制的文字、物品和末个终局为验收依据；截图只作辅助，不以截图数量或齐全与否阻止导出 03。辅助截图按正式视图、事件类型与结构化字段识别，不绑定 dispatch 序号或中文台词。
- 一级回放默认不截图、不要求布局图审阅；仅在显式传入 `--screenshots` 时收集辅助图片，此时须先查看 `draw_player_transcript_layout.py` 的规划图并传入 `--layout-reviewed`。二级 UI 专项仍须实际截图查看。
- 二级 UI 选段截图用 `tools/capture_story_ui_samples.gd`：从本轮正式引擎 JSONL 按剧本行 ID 与卡片、表单、选项结构定位少量片段，仅重放必要视图、状态和现场上下文，用正式渲染器保存图片、来源清单和可见文字到 `artifacts/basic-ui-system/ui-rounds/<round>/`。运行时传 `-- --round=round-01`，默认 1280×992，另可传 `--width`、`--height`；不能使用 `--headless`。截图须由独立验收 Agent 实际查看，选段截图不代替一级全程记录或三级盲玩。

- `PlayerTranscript` 的正常路线在同一日同一时段不重复询问同一人物；时段变化后可以再问。此选择仅减少验收路线的重复操作，不改变正式游戏允许的询问次数。
- 议事立场预览与正式方案共用剧本规则定义（作者、文本、标记），不得在预览代码保留另一套剧情方案或来源。

- 玩家文字采集对已绘制、完整可见的空输入框记录其占位提示；输入有值时只记录实际值，不把后台提示混入正文。
- 表单摘要优先使用事件中的 `template`，将 `{内容}` 绑定到当前选项或未选提示；无模板时兼容 `label`。摘要文案仍来自剧本，界面不另写章节标签。
- 二级选段表单另存选择后的截图：通过实际页签与选项按钮检查摘要和作答按钮状态，只选列表中的可用项，不向引擎提交答案；清单记录这些界面操作，不能作为游玩通关证据。
- 选段工具对可滚动的模态窗另存滚到底的图片与滚动位置；短窗口复核使用独立轮次目录，核对固定抬头、末项和页脚，不覆盖此前证据。
- 对白面板以视口底边和场景中声明的底部留白定位；选项增高及视口高度变化时保持同一底边，不使用固定屏幕纵坐标。标准与较矮逻辑视口分别保存选段截图，不能把逻辑视口测试等同于窗口拉伸测试。
- 表单抬头由 `FormPanel` 持有并更新；正式表单通过 `PaperModal.pin_header()` 将同一节点放在滚动区上方，字段留在滚动区。无抬头表单和组件独立预览沿用原布局。

- 消息的发生日与时段保存在 `NewsItem` 的呈现元数据中；转述模板按事件发生时点绑定，不将呈现时间写进 `Claim` 身份以改变社交去重。完成动作不等于产生后果，完成消息由实际生效的机制分支登记。台账详情只展开真实台账消息，人物的登记状态按本人对应夜晚的取用与登记数量选择剧本模板。

- 三级实时录像可用 tools/record_blind_session.py run --session=<fresh-id> --agent=<name>，以该 Godot 进程的唯一窗口标题通过 FFmpeg gdigrab 捕获 15fps H.264 CRF29；启动器后台运行，确认 recording.json 为 recording 后才开始输入。stop 子命令发送正常封装请求并验证 MP4 可解码；禁止以截图拼接或 --write-movie 替代实时录像。无游戏回环设备时记录无音轨，不采集麦克风。录像默认保存在 artifacts/basic-ui-system/level3-6_1-sol/<session>/；停止封存时同时复制完整输入和界面记录到录像目录 session/，原始记录保留在驱动的 artifacts/remake-belief-replay/blind-<session>/。


- 第一章重演以结构化事件（kind、who、subject、object、room）传给界面；行为进度由重演结果字段记录，界面按事件种类决定播放节奏，不匹配显示文案。事件句型、心声与控制提示只从 `.story` 的文案表读取。
- `话语` 与 `试验句` 的 `句尾含义` 声明片段听闻的含义（开门、求救；空值表示无）；编译为 `TailWantsOpen` / `TailHelp`，运行时不从整句或句尾文字推断。
