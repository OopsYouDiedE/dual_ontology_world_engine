# Godot 游戏层维护与路径规范

本目录为 Godot 4.6.1 .NET 游戏宿主，负责场景树、渲染、房内空间与碰撞、实时循环、UI 和游戏内容。通过现有 ProjectReference 单向引用 Core；Godot 类型、游戏专属状态和素材完全由游戏层管理。场景沿用现有对象绑定，保持简洁直接的架构。制作验收统一遵循 [设计规范](../docs/design-standard.md)。

**架构与同步规则**：本目录发生架构、路径或管线规范调整时，必须同步修改本文件（`godot_csharp/AGENTS.md`）；严禁将剧情写入宿主代码，剧情仅允许存在于 `.story` 和译文中。C# 与 GDScript 中的设定与逻辑查验统一通过剧本编译物与词汇表，不得硬编码剧情台词或设定。

- 保存接口：`Shared/Story/WorldSession.Save.cs`、`SessionPersistence.cs` 提供完整空闲会话候选、进度值的闭合协议、版本化随机流及原子 Store 发布；重复载入相同字节保留对象与恢复 epoch。外部章节宿主必须注册保存切点 guard，不能仅凭 Core active 为空保存。
- 章节适配：`Awakening/Script/Chapter1Save.cs` 与 `Awakening/Night/MorningSave.cs` 保存实际章节进度、时钟、生态游标与引用；恢复使用专用构造，绕过初态、观察、效果和 fiber。`Shared/Game/GameSession.Save.cs`、`Host/GameEngine.cs` 暴露无 UI 的导出/载入 API；当前仅支持第一章空闲点，其余章节与活动选择、回执、走位、流程明确 `UnsupportedSavePoint`。
- headless 保存检查：`godot --headless --path godot_csharp --script res://scenes/Shared/check/check_session_save.gd`；实际推进第一章后保存，启动第二个 Godot 进程恢复并比较连续推进结果。`tools/verify.py` 的 `save` 步骤执行该检查。纯 dotnet 诊断入口为 `dotnet run --project godot_csharp/scenes/Shared/check/SaveCheck/SaveCheck.csproj -c Release -- write tmp/core-save-check` 与同项目 `-- read tmp/core-save-check`。不替代 UI/盲玩验收。
- 保存检查也核对 WorldSession 中类型化权威事件的全部字段/因果与重复载入不再登记事件；事件身份和权限由 Core 运行时协议保存，宿主不自行复制或补发。
- AI 决策的经历、失败/无进展代价、预期信息组合与中断重评使用 Core `SubjectiveDecision` / `ActionLearning`。玩家席位保留公开候选、实际站位、几何反馈及角色声明预算的适配；世界反应、日常人物、策略方法与第二章人物在实际开始/完成/失败/取消处反馈，不能把路标选择当作完成调查。日常可行候选共同参与评估；显式离场流程仍保留阶段门控。经历以主体认知观察保存，宿主不新增一套失败禁令或读取别人的经历。不同角色的推理深度仍由原声明决定，默认调用使用浅层预算。
- 决策迁移的机制回归运行 `PlayerControllerCheck`、`StrategyIntegrationCheck` 与 Core `SubjectiveDecisionRegression`；中性夹具验证真实失败后改选、保存恢复、主体隔离、已见路线和实际移动反馈。真实虚拟侦探会话仍由主代理运行 FullTest，不能用机制回归替代通关证据。
- 玩家席位选择带位置的现场 `interact` 只登记待接触；实际 `WorldView.field_event` 的 map/site 与本次所选输入匹配后，经 `Main → AgentPilot → GameEngine` 反馈，才消耗调查尝试。重新空闲、候选替代或停止前尚未接触的路线记录失败，保留成本但不冒充已调查、也不耗尽未知线索的尝试预算。调查次数按空间和目标稳定保存；接近失败保存到 `contact` 方法，以选择时本人和公开目标位置的 64px 格作为 Core 经历 Context。同格保留失败成本，实际看见人物移位或本人换格后使用新路线条件，位置变化不重开已调查次数。回执只消费当前待接触 token 一次；非现场笔记/表单输入仍按普通选择记录。此反馈复用既有 Core 经历，不查询不可见人物的新位置。
- 现场房间同步只接受非空 `entered` 回执；门槛、门洞上保留最后实际进入的逻辑房间，与视野揭示一致。玩家席位的导航记忆仅采样当前可见地面，不读取完整实际导航网格或未知落点碰撞；未见方向通过真实 WASD 与碰撞取得反馈。调查重试按对应材料的主观证明变化重新开放，不能因无关新问题或新材料清空所有尝试；同名人物话题菜单按交谈对象隔离，公开面板入口以 `purpose=panel` 声明，按空间与入口记忆，不随人物或物件交互刷新，也不与同标签人物话题混用。当轮等价命题共享证明请求，命题与缺项使用 Core `Ast.Equivalent` 比较完整结构；预算耗尽等不完整结果不能刷新尝试记录。
- FullTest 在现场动作前等待正常绘制帧，记录 `decision_frame` 的 token、实际脚底位置、入室/区域回执及 `interior`/`feet_region`，以区分门槛与尚未绑定逻辑房间键的视觉区域；调查、入室或可见区域变化及截图间隔到期时保存关联 token 的 `decision` 图片。`PlayerDecision.Alternatives` 只含公开候选及本人评分依据，用于对照截图分析折返；必须实际查看对应图片，不能用候选分数推断画面已正确。诊断回执与独立盲玩证据分别报告。

- `approach` 短路标的 `travel_distance` 只计本次输入沿已见路线到所发 point 的长度，上限与 128px 路标保持一致；不能在每段再次扣到最终目标的全程成本。直接现场 `interact` 仍估计真正接近目标的整段已见路线并扣除接触半径。导航机制回归须区分路标单段与实际接近目标，不为减少成本读取未知几何。

---

第二章 `Prototype/Chapter2Entities.cs` 按剧本稳定 ID 登记同一会话的实际对象引用；`Chapter2Bindings` 只解析登记表并检查类型。声明产生地点、物件、人物、群体及通行集合；现有具名机制通过兼容枚举访问，枚举不充当集合来源。新增地点与物件不要求增加枚举或字段绑定；新增地点的具名行为与通行接口仍有存量枚举边界。登记拒绝重复 ID、未知引用及类型冲突。

第一章人物/地点集合与兼容序号、重演通路从编译内容读取；六项重演变量由 `Night/ReplayDomains.cs` 读取取值编号、显示与绑定。旧取值编号和索引不得重排；新增取值追加并绑定现有机制。变量结构及具体机制的进一步通用化另行迁移。

实体登记合同检查：`python godot_csharp/tools/check/test_entity_registry.py`，再运行同命令的 `--prepare` 与 `dotnet run --project godot_csharp/scenes/Shared/check/EntityRegistryCheck/EntityRegistryCheck.csproj -c Release -- tmp/s03-check`。夹具仅扩展临时剧本，覆盖新增地点/物件、同一引用、未知/重复 ID、类型冲突、数字 ID 兼容边界、六项取值域扩展及原六字段序列化合同；不替代 Godot 或游玩验收。

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
| 小组件 | `UI/Components/` | `Paper.gd`（组件工厂，只按类型变体创建、读主题色）、`PaperButton`、`PaperLabel`、`PaperRule`（细线／双线／短线）、`PaperModal`、`PaperCheck`（选项行）、`PaperEntry`（条目行）、`PaperStamp`（印戳）、`PaperTally`（计数）、`PaperLedger`（空白账页）、`PaperPixelText`（像素字：半分辨率无抗锯齿渲染的标题类大字）、`PaperBook`（摊开的册子：横格对开页、书口索引签、页脚）、`PaperOption` / `PaperChoice`（圈选项与一行圈选项）、`PaperScale`（刻度尺）、`UIAppearance.gd`（外观设置落地：只改三份主题里已有条目的字号、颜色、纹理与字形来源）、`DialogueResponse`、`ReplayBubble` |
| UI 面板与子系统 | `UI/Panels/<面板>/` | `Dialogue`、`Menu`、`Card`、`Form`、`Notebook`、`Manuscript`、`CampusMap`、`Ending`、`HUD`、`Field`（提示层、回放层、视野遮罩）、`Settings`（设置册：凡例 / 操作 / 书签）、`Title`（油灯下桌上的册子；封面在子视口里排版后旋转显示） |
| UI 总装 | `UI/UISystem.tscn` | 整合各面板；只读宿主事件与快照，用 `ui_event` 回传 |
| 宿主 | `Host/Main.tscn`（主场景）、`Host/GameEngine.cs`、`Host/PlayerViewTrace.gd` | 唯一承接 C# 会话的节点 |
| 样式 | `UI/Styles/*.tres`（`UITheme`、`DialogueTheme`、`TitleTheme` 由 `tools/build_ui_theme.py` 生成，改外观先改生成器再重建；纸纹与横格页纹理由 `tools/build_ui_paper.py` 生成，标题页的桌面、灯光、光晕与布面封皮由 `tools/build_ui_desk.py` 生成） | `UITheme.tres`（纸页主题：语义色 `Paper/colors/*`、全部类型变体、自绘控件的颜色与尺寸）、`DialogueTheme.tres`、`TitleTheme.tres`，以及开发场景的 `MapBrowserTheme.tres`、`WorldScriptPreviewTheme.tres` |
| 设置 | `Shared/Settings/GameSettings.gd`（自动加载 `GameSettings`） | 图像、语言、声音、按键与无障碍设置的读写、生效与持久化（`user://settings.cfg`）；界面只经它读写，外观类设置由 `UI/Components/UIAppearance.gd` 落到主题 |
| 编辑器总览 | `UI/Workbench/` | 各面板的预览页 |

现场（地图视图）在 `Shared/Field/`，各章地图适配器在各章目录；现场用到的物件外观名等文字由 `Shared/Field/FieldVocabulary.gd` 从剧本编译物读取。
两章的 `现场表现` 块声明稳定身份到演员节点、资产、地图、站位及调查对象的绑定；第一章编译到 Content/Layout，续篇编译到 `design.presentation`。地图适配器 `setup()` 返回成功状态，缺失身份、资产、锚点或痕迹快照须报错并终止。复合精灵按资产声明的局部区域与词汇键读取；痕迹按权威快照与当前视线绘制。`presentation_anchor` 是宿主表现元数据，不进入 Core 状态或认知。
第二章调查对象在世界建立后绑定为实际 `Essence` 引用，区域集合从声明派生；笔记图片另以实际对象绑定声明，明确空值表示无图，不要求该对象可调查。现场人物肖像取实体的 `sprite` 属性，地图归属及集合站位取表现声明。第一章地图生成器从布局读门主人、演员起点与动画资产，只保留书架槽的几何，不按节点名决定痕迹内容。
普通家具悬停复用地图适配器的 `furniture_hits` 与现场词汇表，只对已绘制且在视线内的精灵显示外观名；剧情热点保持优先。

`stage_action` 从开始走位到动作结束持续占用人物，异常或取消时也归还；侦探被演出占用时快照关闭 `player_control`，现场输入循环不得覆盖演出姿态。

第一章流程中的 `stage_actor:<who>:<anchor>` 等待走位回执，锚点支持 `meeting:`、`post:`、`site:` 前缀；`stage_action:<who>:<anchor>:<anim>:<facing>[:<seconds>[:<after>]]` 到位后播放动作并等待指定秒数，再保持后续姿态（默认 0.8 秒、idle），走位失败则跳过动作。`post:<who>` 非阻塞启动单个同伴的岗位走位，`posts` 保留全体岗位走位；这两种命令用 `Start(..., immediate: true)` 先输出首个走位事件再输出后续对白，避免界面等待对白回执而延迟起步。流程负责安排这些指令与场面的先后。

第一章阶段的进入规则可以引用流程；会话按实际房间进入执行，重复演出的条件由流程自行管理。人物对话中的协助邀约由会话并行调度走位，流程参数保留当前交谈对象，走位期间不占用侦探控制。

流程参数来源在每次继续时重算并覆盖携带的派生值；交谈对象作为显式输入保留。协助邀约使用可达的物件邻近点，完成后复用增量证物卡。地图连续落点与导航网格共用角色脚印的通行判定。`StoryPacketCheck` 覆盖携带旧参数时的重新提问、重新裁决与步数推进。

第一章阶段的 `时刻 <分钟>：流程 <名>` 编译为有序 `times`；会话在结算共享时钟与远处生态后、现场空闲时执行到期流程，按阶段与时刻记入进度，跨过边界和阶段再进入均不重复执行。`clock.door_open_minutes` 只记录首次开门，续章用该值与其起点取较大值。

`enter:<人>:<地点>` 通过 `stage.spawn` 与现场回执登记来访演员；名称、立绘、交互热点和 `enter_<人>_<房间键>` 锚点来自剧本。来访演员加入现场与交谈注册，现实位置由几何同步并暂停原远处日常；不加入 `Person`、四人案件、回忆、重演或同伴协助集合。行走帧复用共享角色资产。第一章地图保持原有通行边界，门洞邻近提示与开门显示由剧本数据和快照驱动。

生态编译为晨间、出区日常补入其引用的续篇地点声明，供低精度推演使用；不因此增加第一章现场地图或继承续篇场面。

第一章普通交谈、协助场面未单独声明时长时，使用对应行动时长，避免再套用默认剧情场面时长；显式声明的剧情场面仍按编译数据结算。

第二章的时段流程和嵌套选择由 `.story` 编译到 `design.flows`、`design.schedule`；`Chapter2SessionFlows.cs` 执行步骤并等待正式对白或菜单回执。场面与耗时动作由流程组织，会话不得另列一套时段场面表。`通行` 编译到 `design.access`，以稳定 ID 声明边界、起终点、可见范围、初始位置及召集消息；`Chapter2Access.cs` 经 Core 提交改变边界状态，再派生实际通路。人物只因当地观察、复查、消息或行动失败更新通路认知；开门不搬动人物。流程的 `summon:<id>` 经社交请求与自主决策提交 Travel。变更、召集与即时调停写入有序 `FreeEvents`，夜间快照分别保留权威权限与人物通路认知。人物菜单通过 Core `SocialSystem` 的只读预览排序，实际收到的请求记录相合分供自主决策复用，拒绝在时段结算之前处理。

`规则有效期`、规矩的 `有效期` 及场面的触发器 `有效期` 接受 world/chapter/interaction；它们独立于实体和候选 scope。`RuleLifetimes` 经 Introspection 精确移除匹配 lifetime/owner 的规则，同时撤销同范围的实体规则源，保留投影、记忆与其余规则。连续世界交接先确认所有已有人物的动作已完成；重复交接返回同一个活世界。剧本与编译器修改须从源重新生成并检查一致性。

第二章的 StructuredEvents 保存实际行动回执及对象引用；请求使用稳定 Topic，口供、履约与地点从回执查询。事件的因果引用连接实际执行回执；转述消息保存其来源事件，玩家只读本人亲见或听说的具体事件，链描述过滤未获知步骤。痕迹分开保存结构化事实、观察权限和渲染文本，候选与比较只读取获知的字段，痕迹事实使用玩家可辨的类别，不暴露原始隐藏时长；比较结果携带枚举状态。CaseTraces 相等比较所有字段与列表值。中文日志仅用于呈现；第一章侦探规划器用实际可见行的稳定来源 ID 查询线索，未署来源的显示文本不提供命题证据。
`dotnet run --project godot_csharp/scenes/Chapter2/Game/check/EventCheck/EventCheck.csproj -c Release` 验证结构化证物比较、认知来源过滤及日志/对白/人名显示替换后地点、候选、回应与行动选择保持一致；依赖正常剧本编译产物。

两章的 `表达` 数据块由既有剧本编译器生成；共享 `ResponseCatalog` 只筛选声明条件与优先级，不修改世界或认知。说话人、听者与条件读取器由会话提供，第一章仍由 `SpeechPolicy` 选择言语行为。来源及理由显示由第一章表达模板绑定 Core 结构化结果；第二章称呼使用身份占位符与说话人认知条件，禁止全文替换称呼。无适用回应返回 `none`，章末总结不把方案条款当作行动事实。
表达合同检查：`dotnet run --project godot_csharp/scenes/Chapter2/Game/check/ExpressionCheck/ExpressionCheck.csproj -c Release`；覆盖译文替换不改判定、身份知识与听者条件、明确无反应、结构化来源/理由及总结只读性。它不代替一级、二级或三级验收。
表达声明编译检查：`python godot_csharp/tools/check/test_story_expression.py`；覆盖中性内容的译文独立性、条件与优先级类型校验，不改既有回归预期。

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

- `.story` 的认知命题、认知接线和推知关系由 `tools/story_knowledge.py` 校验并进入章节 Content 的 KNOWLEDGE；不从裁决或隐藏状态生成。Shared/Story/AcquiredPropositions 仅在实际呈现卡片、台词、可见选项或现场候选时，经 Observation 创建本人命题和来源节点；候选不自动采信，证言按说话人、言语行为、声明可靠性与本人的信任采信。来源物体与记忆内容独立，来源失效只撤销可出示性；实际展示的出示选项也更新可出示性。条件规则按主体去重，原始词形保留且 canonical 按 NFC 规范化。
- 第一章 `Chapter1Knowledge.cs` 接线正式显示事件、打开手稿及候选表单当前页；重置和恢复重新绑定观察适配器，既有认知由存档保留。表单提供通用 purpose/selected 元数据供代理区分陈述、出示、变量、页签和提交。轨迹增加每份命题的证明值、完成度、本人依据与步骤；私有主体句柄不公开。

- `Shared/Story/PlayerController.cs` 承接 `.story` 的角色操控声明；`玩家` 带本人操控规则时编译为 `mode: player`（不带规则保持既有默认产物），`代理` 编译为 `mode: agent`。规则类别只接受通用输入种类、公开新鲜度与自身选择历史，不支持行动序列、章节身份或源位置。编译器位于 `tools/story_controller.py`，由 `story_script.py` 统一调用。
- `推理预算：深度 N 展开 N 假设 N 论证 N 求知 N` 是通用角色参数；省略时使用浅层小预算，不按角色名分支。代理将可见字段标题／选项文本与本人已获知词项或明确名字绑定，调用 Core `SubjectiveReasoner` 比较出示材料的论证支持、公开变量组合的假设预测与未知命题的调查价值，再与 `DecisionSystem.Assess` 合并；可提出命题可来自未接受的 Infer/Think；证明只使用本人已接受的信念，不把普通 Procedure 或规范偏好当作事实。没有已获知命题模型时明确记录 `no_acquired_propositional_model`，不从隐藏答案补齐。决策公开实际匹配的操控规则、本人已知依据与推理展开／停止／假设数；规则依据在更新本人选择历史前固定。每步决策同时记录所选命题、本人材料、前提到结论的推知链和缺失命题；主体私有句柄只在 Core 内使用，不进入公开轨迹。第一章的公开命题接线由实际呈现入口获取；机制检查不代替正式连续会话的推理通关记录。
- `IPlayerRoleSession` 只由可信章节适配器提供当前玩家角色的不可变认知和正式游戏时刻。代理只接收 `PublicPlayerObservation` 与该角色认知，使用 Core 的快照 `DecisionSystem.Assess`；候选评估的临时节点不回写角色。`PlayerCandidateSession` 持有 token 到原菜单、表单与现场输入的映射，经同一 `GameSession` 分派执行；刷新、消费、未知/过期 token、预算或提交异常使 token 失效，错误关闭边界。当前章节只承接其表现声明指定的玩家角色，其余角色操控声明明确拒绝，不静默借用玩家身份。现有宿主存档只保存章节认知／进度，未保存代理历史、随机状态和候选；声明代理的会话 Save/Restore 在发布前以 UnsupportedSavePoint/host.controller_state 拒绝，避免不完整恢复继续使用旧 token。真实玩家空闲存档通路保留。
- `PlayerPanels` 由可信宿主按共享面板的呈现合同投影笔记、手稿与学园图；只保留可见问题/选项、已知地点、已经展示的卡片和反馈，私有字段/隐藏变量/未画出的对照详情不进入代理。分标签表单只投影当前字段，页签切换经本地 token 更新，不向引擎提交作答；允许重新编辑已选字段，提交仍核对所有必填项。手稿变量按公开选项索引生成合法 token；重演沿正式 `replay_vars/replay` 通路，回放不跳过，地图沿正式 `travel` 通路。按钮文字从既有共享面板资源读取，不新增 UI 或章节文案。开启面板和作者自动展示产生正式 `opened` 回执。
- 代理走位候选可提供实际可见目的坐标 `Location`，与私有输入 payload 分开；操控者按 64 单位空间格记录尝试，避免浮点坐标抖动产生无限新选择。有多个公开候选且无新采信材料时重复输入按角色的未尝试偏好权重递减；对应材料的主观证明变化后重新评估相关访问。提交后清空上一表单的证明轨迹。
- 实际听到人物发言时，将作者声明的本人初始信任写入说话人认知投影；已有信任修订优先，不因重听覆盖。答辩提交要求陈述具有本人完整、非假设且无冲突的 True 证明；需要出示时材料必须参与该陈述的依据链。未选陈述的证物自身事实不算答辩支持；试验假设与普通文本表单沿原提交合同。
- 答辩的出示候选只选参与当前陈述完整证明的可出示材料；菜单可声明通用 `purpose=argument` 表示作答入口。当前问题已有本人完整证明时，代理优先返回对应的已见现场机会并作答；现场坐标仅来自当时可见热点，移动按软方向偏好选择现有短步，碰撞与绕行仍由正式物理循环裁决，不读取未见地点或预置路线。
- 操控声明的编译合同沿用 `reasoning.argumentWeight/informationWeight`；加载器显式保留这两个字段，不能因世界声明的 snake_case 策略回落到默认权重。认知合同检查经正式声明加载器取得推理参数，再检验调查与答辩选择。
- 人物现场的 `person:` 运行时前缀经通用来源编号转换后对接作者声明；只给实际可见入口记录机会，不预先采信其内容。调查优先当前问题的缺失前提；对应材料的证明变化后可重试相关旧入口。普通试验提交沿输入合同计分，不获得已有完整论证的提交奖励。驱动无进展期限只用于可输入的空闲阶段，正式演出/重演播放期间仍受墙钟总预算约束。
- 现场/选择机会只绑定完整公开名称；子菜单包含人物名或物件名不继承父现场的信息价值。具体选择必须从自己实际呈现的接线取得机会，其他已知词汇继续沿原短语匹配。
- AI 坐席：`GameEngine.ConfigureAiSeat(seed, maxSelections, maxObservations)` 让 AI 坐上当前玩家席位，按该角色在 `.story` 中声明的操控规则决定；角色没有操控规则时以 `agent_rules_missing` 拒绝。两章的世界级角色声明合并读取，跨章保持同一席位。`Host/AgentPilot.gd` 是席位节点，由 `Main.enable_pilot()` 挂上：现场空闲时提交视线内调查点、可达走位（含视线内 128/256 长步与本人已见区域的交界 `purpose=frontier`）、方向键、等待与面板快捷键；决定后经正式界面与现场把动作做出来——台词逐句推进、选项落下前停顿并合上弹窗、表单显示所填项、现场动作经引擎回显 `player_field` 由正式寻路/碰撞/按键执行，面板经 `player_panel` 打开。席位坐着时界面的普通操作不代替角色决定（`Main._seat_input`），对白读完回执交回席位，现场回执走 `DispatchField`。候选 token 在无新呈现的刷新中保持（同一选择未变即同一 token），被消费或撤下即失效；宿主最近一次现场/面板候选在生命周期刷新中保留到被消费或替换。席位记录 `seat_wait` 门闩原因、`decision`、`act`、`shown`（画面实际显示内容）。实际提交前再次 Peek 当前 token；自己刚选的 token 在等待期间被更晚的非空公开候选替代时，记录 `superseded_action` 并回到下一次正常观察，不执行旧输入；相同观察、空候选、未选 token 或其他边界错误仍停止。
- 控制器（`SubjectivePlayerController`，任何坐席角色通用）的探索与收手规则：走位按 64 像素格记录走过的地面，普通已见地面不因尚未踩过而获得新颖性；未知落点及已见交界保留探索价值；已见区域交界只核验已见地面，未知一侧通过实际按键和碰撞探索，首次到达前有额外吸引；见过的人物/物件在原处不再出现时忘掉旧位置；当前问题缺前提时，按已见但尚未执行的具体方法返回来源；已查过且没有未做方法的地点不因固定第二次检视而继续获得求知奖励；答辩须出示时，只凭记忆的证明不算可答，出示不支撑陈述的材料不得分，答不上时“暂且不答”不另扣分；卡片选项 `purpose=destroy`（毁坏物件）视为丢失可出示材料而扣分，只有本人规则偏好毁坏的角色才会选；返回/合上类选项（`purpose=close`）不计重复惩罚。
- 边界探索按 32 像素格单独记录尝试，避免与房内普通走位合并；只核验边界已见一侧，接近后向未知方向真实按键尝试，不查未知落点是否通行；已见地面的点击只沿记忆网格规划，实际输入受现场碰撞约束，不预置到场回执。
- 坐席观察通过 `GameEngine.AgentLocate` → `GameSession.LocateAgent` → `PlayerCandidateSession.Locate` 提供实际身体位置及宿主分配的不透明坐标空间标识；地图源 ID 不进入公开观察。控制器只把实测位置计入已走地面，未产生位移的移动尝试按本人回执累积扣分；不同空间的地面、交互地点与走位历史分开。只有对应方法自身命题的证明状态变化才重开该输入；本次执行获得的结果只更新基线，不重开自身，不因父入口、新问题或无关材料刷新整份人物菜单，也不抹去碰壁和走位经验。公开候选的 TravelDistance 从本人已见路线估算，无路线时只用已知坐标距离下界，经 Core 原有 Cost 参与评分；端口拒绝负值和非有限距离。朝已见线索或答题机会的方向评分在本轮候选远近范围中归一化，保留远近差异，避免提前截断让大量方向同分。人物偏好和能力预算继续来自声明，不通过具体路线或答案调参。
- 坐席按空间记忆实际见过的地点与地面通行；`PilotNavigation.remember_grid` 只以当前视线更新格子，未知地面保持封闭，`approaches` 提供通往已见地点的短路标候选（`purpose=approach`、`value` 为公开地点名）。控制器按本人问题评价地点，路标的预期材料价值与目标的实际检视记录绑定，不将不同路标当作新调查，也不重复叠加地面探索与直线方向奖励。必要返回不因已有走位经验累积无界惩罚，目标的检视经历及碰壁经验仍保留。共享导航以 8 像素中心采样，保留连续身体可通过的窄通路，端点按网格自身分辨率选取；仍由正式点击、碰撞与回执执行。
- 章节结束清除玩家已结束的当前问题标记，保留已获知命题和信念；耗时动作结束后，若侦探实际空间与当前地图的声明空间不同，章节适配器按实际空间输出正式 `scene` 事件再呈现行动回执，不把旧地图几何覆盖到权威状态。
- 第二章现场按地图出口已有形状及实时通行快照创建关闭出口的碰撞阻挡，并同步导航网格；通行变化时延后刷新几何，开放后撤销阻挡。拒绝换图时取消点击路径并退回本图最后的有效位置，键盘移动与点击移动共用实际碰撞，不允许从关闭的出口走到图外。
- 全量测试：`python godot_csharp/tools/full_test.py --name <新名> [--time-scale 4] [--max-seconds 1800] [--no-progress-seconds 45]`，启动 `scenes/Shared/check/FullTest/FullTest.tscn`（子节点即正式 `Main.tscn`）。只由主代理运行，同一时刻只跑一个 Godot。画面即正式游戏画面；轨迹与截图写入 `tmp/full-test/<名>/`。`--time-scale` 整体加快引擎时间并同比提高物理步数（单步时长不变），不改 Core 与事件顺序。无公开进展（台词/文字/非走位候选/64 像素格位置均无新内容）、缺候选、未被更晚公开观察替代的 token 失效或引擎错误即停并记录原因；卡住立即停下修复后从头再跑。外层 Python 看门狗在墙钟预算外再给 120 秒后强制结束。运行前须先对工作区执行一次 `godot --headless --path godot_csharp --import`。
- 代理导航中性合同：`Godot --headless --path godot_csharp --script res://tools/check/check_virtual_navigation.gd`，检查视野外仍可尝试短步、未知路线不预读、长步保留正式可见寻路，以及已见 U 形绕行、未知地面封闭、隐藏变化不改记忆和不同网格分辨率/偏移；不注入到场回执。
- 呈现替代中性合同：`python artifacts/detective-agent/chapter2-20261011/verification/presentation-supersession-r07/run_fixture.py <Godot控制台程序> <新运行名>`；独立 SceneTree 使用正式 AgentPilot 与中性宿主，验证等待期间新公开候选替代旧输入后正常重新决策，以及其他边界错误仍停止。日志不可复用，不代替正式窗口或盲玩。
- 关闭出口中性场景树合同：`Godot --headless --path godot_csharp --script res://tools/check/check_campus_access.gd`；自行构造中性地图，验证实时通行的实体碰撞与导航、重新开放、重复快照、独立出口和室内拒绝换图的退回与点击取消；同时使用正式角色身体验证窄通路和邻墙碰撞、真实按键的墙钟保持与主动停止释放。不调用章节 setup，不注入到场回执，不代替 UI 或盲玩验收。
- 认知接线合同检查：`python godot_csharp/tools/check/test_story_knowledge.py --prepare-fixture tmp/<新目录>`；随后构建 `scenes/Shared/check/KnowledgeCheck/KnowledgeCheck.csproj`，用 `dotnet run --project godot_csharp/scenes/Shared/check/KnowledgeCheck/KnowledgeCheck.csproj -c Release --no-build -- --fixture tmp/<新目录>` 检查命题与信念分域、证言来源、物体失效后的记忆、隐真变形以及删去中性剧本卡片接线后的回忆路径。不写正式生成物，不代替正式通关或 UI 验收。
- 代理编译合同检查：`python godot_csharp/tools/check/check_player_controller.py`，并核对正式剧本中声明的玩家席位带本人规则。运行时边界与隐真变形检查：先 `dotnet build godot_csharp/scenes/Shared/check/PlayerControllerCheck/PlayerControllerCheck.csproj -c Release -m:1 --disable-build-servers`，再 `dotnet run --project godot_csharp/scenes/Shared/check/PlayerControllerCheck/PlayerControllerCheck.csproj -c Release --no-build --no-restore`；等待构建退出后再运行。该检查项目只链接正式模块，不作为盲玩、UI 或剧情验收。Godot 项目排除其独立入口源码。

- `Shared/Story/Examples/WorldScript/*.story` 是独立机制示例的作者源；使用正式 `tools/story_script.py` 按章节依赖顺序编译为 `generated_world.json`，不再维护 `.world` 示例。`ExampleWorld.Load(path)` 只接收编译物并创建 `WorldSession`，示例工程只引用 `ChapterRuntime`，不再作为第二章 `IWorldScript` 扩展 DLL。作者预览仅枚举已加载声明，房间与携带对象位置从同一运行时对象读取；文档缺失或加载失败不填入样例。
- 示例检查：`dotnet run --project godot_csharp/scenes/Shared/check/WorldScriptCheck -- <编译物路径>`；编译命令为 `python godot_csharp/tools/story_script.py godot_csharp/scenes/Shared/Story/Examples/WorldScript/chapter_01_arrival.story godot_csharp/scenes/Shared/Story/Examples/WorldScript/chapter_02_morning.story -o <编译物路径>`，加 `--check` 校验一致性。预览传输自动检查使用 `Shared/check/WorldScriptCheck/preview_ui_check_01.gd`，可通过 `-- --document=<编译物路径>` 使用临时编译结果；该检查不截图，不代替 UI 专项验收。
- 自动检查与固定路线工具的情境、路线和封存预期存于 `scenes/Shared/check/Fixtures/`，以稳定选项、行号、证物或夹具键查询；读取器 `FixtureData.cs` 仅由检查/工具项目链接，`fixture_data.gd` 仅由自动检查加载。黄金预期不从被测算法或正式真值表在运行时生成。第二章 `Prototype/Bots.cs`、`Checks.cs` 只由 `Prototype.csproj` 编译，正式 `Chapter2Script.csproj` 排除这两份开发代码。
- `PlayerTranscript` 的调查、相遇/答辩输入及连续日程由 `Fixtures/player-transcript.json` 的路线步骤提供，工具只分派输入、等待与记录，保留两章结束断言；正式输出的房间到场回执仍由空间适配器处理。可在仓库路径后传 `--output <目录>`，把本轮采集放入隔离目录；省略时沿用原记录目录。隔离采集与封存预期各自保存，复测不得覆盖旧证据。

- 源码内容审计使用根目录的 `STORY-HARDCODE` 标记约定，分别记录正式代码、夹具、工具、示例和编辑器预览。剧本生成的 C#/JSON 只登记来源，不手改；专用绑定与剧情因果分别分类。仅加标记时验证原有源码不变、宿主构建与修改过的 GDScript 语法，不将其当作一级、二级或三级验收。

- 第一章 Godot 自动检查统一通过 `Awakening/check/chapter1_fixture.gd` 加载 `Host/Main.tscn`，发送标题页 `start` 并等待正式视图握手；需要略过开场时实际推进对白，不使用 `skip_intro` 元数据。宿主或视图加载失败立即退出非零状态。`Shared/check/ChapterProbe.cs` 仅供检查，反射访问统一会话持有的 `IChapterProbe` 并复用宿主 Variant 转换；正式输入接口不开放状态注入。自动检查结论不代替任何一级、二级或三级验收。
- 第二章 `Chapter2/check/check_flow.gd` 通过 `chapter2_fixture.gd` 复用上述 Main 夹具，先用调试跳转推进第一章终局，再以章末卡 C 键触发连续会话的视图切换与 `view_ready`，检查首个可操作状态。后续输入和 UI 回执仍经过 `Main.dispatch`，不再加载旧章节宿主、调用引擎直通接口或单独注入继承快照；地图加载失败和流程等待超时须报告并退出非零状态。
- 社交请求的流程 AST 使用发言者内部世界的引用提交；接收者认知只用于筛选已知目标，发言传播再将请求映射到接收者内部世界。第二章流程回归按正式日程检查开场与首课，检视物证后再验证同行结算。
- 第二章规矩、规则格的主体/条件/行为及社会行动由剧本声明，`tools/story_ast.py` 复用表达式解析器校验类型，`Shared/Story/ScriptAst.cs` 绑定到已有 `Condition/Effect/Rule`，不注册专案行为回调。规则格以作者选项 ID 选取 AST 分支；历史条件只读当前引用所属内部世界的观察记录。纯元信息规矩未声明执行块时携带空 AST，不产生行动；显式 AST 行须完整声明条件、模式、效果与权重。未知节点、过程、引用与递归过程明确拒绝。
- 社交菜单与提交共用相合计算；预览只读，提交时重新读取当前条件和目的。声明的 `condition_world` 区分当前物理可行性与本人认知条件，默认 subjective；physical 检查不替人物写入通路知识，收到请求后仍由本人规则选择、Core 当前条件验证实际执行。请求提交须本次 Say 完成并产生新请求，不复用旧接受记录；接受事件与 Core 行动执行事件分离，成功反馈、履约和后果须有 Completed 回执；取消会关闭收到的请求，不能下一轮重选同一请求。成本、目标筛选、承诺期限及履约行动 ID 从声明读取。`receipt_attitudes` 声明正式接受/拒绝后对发送者的态度记录标签；该记忆以 Delivered 事件因果关联接受回执，不表示请求动作已执行，也不参与预览写入。
- AST/请求合同检查：先运行 `python godot_csharp/tools/check/test_ast_requests.py`（同时编译隔离扩展副本），再构建 `dotnet build godot_csharp/scenes/Chapter2/Game/check/AstRequestCheck/AstRequestCheck.csproj -c Release -m:1 --disable-build-servers`，构建成功后运行 `dotnet run --project godot_csharp/scenes/Chapter2/Game/check/AstRequestCheck/AstRequestCheck.csproj -c Release --no-build --no-restore`。覆盖仅改剧本新增规则/行为/请求、预览只读及拒绝/取消/处境变化/不能完成；不代替 UI、文学或盲玩验收。
- 环境法则和劳作配方复用 `ScriptAst` 的 `Condition/Effect`；`WorldLawTransaction` 只允许原子属性更新，拒绝行动、发言和认知写入，实际提交产生无行动主体的类型化记录。整数表达式按绑定时的状态快照求值；主体绑定只读取该主体的认知和自身参数。生命周期回执按声明与实际作用对象去重。外部刺激只把请求交给实际到场的接收者，其回应仍由原有社会与决策机制产生。
- `WorkOccupancy` 按执行对象引用、参与者引用和工位引用记录连续区间；只有不同参与者的成功执行与足够重叠时间能形成配方回执，取消、离场或换执行对象中断连续区间。配方尝试消费后不可用后来改变的条件追认成功。已提交的中间阶段与最终完成标记分开，失败不写完成标记。
- 法则/配方编译检查：`python godot_csharp/tools/check/test_world_processes.py`。运行时检查先构建 `dotnet build godot_csharp/scenes/Chapter2/Game/check/WorldProcessCheck/WorldProcessCheck.csproj -c Release -m:1 --disable-build-servers`，再运行 `dotnet run --project godot_csharp/scenes/Chapter2/Game/check/WorldProcessCheck/WorldProcessCheck.csproj -c Release --no-build --no-restore`；该检查使用隔离的中性机制夹具与正式声明，不代替 UI、文学或盲玩验收。
- `tools/`：素材批量切片、图集生产、场景组装及 Godot 命令行测试工具。
- 表现绑定开发检查：`godot --headless --path godot_csharp --script res://scenes/Shared/check/check_presentation_bindings.gd`；验证改演员 ID 后仍绑定同一节点，缺失节点或玩家绑定明确失败，不代替游戏验收。
- 宿主表现声明合同检查：`dotnet run --project godot_csharp/scenes/Chapter2/Game/check/PresentationCheck/PresentationCheck.csproj -c Release`；覆盖地图改键、缺失肖像/地图、实际调查对象引用与中性非空间锚点，不改既有回归预期。
- 通路与有效期合同检查：`python godot_csharp/tools/check/test_access_lifetimes.py`、`dotnet run --project godot_csharp/scenes/Chapter2/Game/check/AccessLifetimeCheck/AccessLifetimeCheck.csproj -c Release`；验证声明引用、局部通路认知、开门与实际出行分离、跨章混合规则保留和重复退出，不代替 UI 或盲玩验收。
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

- 人物 `策略` 由 `tools/story_strategy.py` 编译；每人 1–3 条，声明对象、姿态、优先度、认知来源、方法类别与本人标签阈值。共享 `StrategyPolicy` 只给当前可用候选绑定章节级 Prefer 规则，不创建行为队列；来源通过正式 Observation 写入本人认知，跨主体候选引用拒绝。
- 第一章策略适配位于 `Night/MorningStrategies.cs` 与 `Script/Chapter1Strategies.cs`；方法/场面/材料/拒绝选项/物件许可均由 `.story` 牵引表绑定。可选 `source_scenes` 将实际说出的场面映射到认知来源；对白完整结束后只为同场可见的人采集来源，远处/隐藏者不采集，重复听闻不重复创建来源观察。全局案件进度只用于测量，人物的停滞判断只取同场可见活动与本人目睹过的材料；报告使用本人 Perception/Recall 历史，经 Testimony 写入听者。初始化不能为可报告材料新增检视全文；正式宿主分享检视时传入几何可见者集合，纯逻辑调用保留同场抽象。未询问即结束才记拒谈，亲见全新材料解除当前拒谈/答错压力，历史观察保留；重复或未亲见材料不解除。亲见和证言分别存储，`known`/`获知` 查询可读两者；试验、协助仍保留各自门槛。
- `每游戏分钟现场秒` 控制自由活动现场的游戏钟；模态阅读、暂停和剧情调度不累计现场时间，余秒进 ChapterProgress 保存。存档内容版本同时覆盖内容产物和 world 产物，人物策略变更使旧版本存档失败关闭。
- 虚拟玩家规则可在末尾加 `文案 "公开子串"`，只筛选公开候选 Label；不读取令牌负载或源位置。第一章驱动的 wait 是真实一秒不操作，不提交章节快进；移动候选标注可见落点房间名。测量保存 player_time、case_progress、strategy_deviation/action、object_changed、world_resolution 事件。
- 策略合同检查：`dotnet run --project godot_csharp/scenes/Shared/check/StrategyCheck/StrategyCheck.csproj -c Release` 与 `python godot_csharp/tools/check/check_story_strategy.py`。
- 集成合同检查：`dotnet run --project godot_csharp/scenes/Shared/check/StrategyIntegrationCheck/StrategyIntegrationCheck.csproj -c Release`；覆盖公开候选筛选、正式编译表类型、证言/亲见边界、物件操作的当前站位复核、主体隔离、存档指纹和策略冷却恢复。默认调用真实 Python 编译器编译同目录中性 `controller-wire.story`，核对操控筛选与预算经 C# 读取后的实际评分；`--controller-wire-json <file>` 可喂入独立命名故障副本，负例预期非零退出，不放宽既有断言。机制夹具的注入状态不属于虚拟玩家测量证据。
- `Host/Main.gd` 仅截留明确列出的测量事件，原始会话与 headless 驱动仍保留；人物实际对白和动作继续经正常显示接口。未知显示事件仍由原界面合同拒绝，不放宽 UI 或统一回归的错误扫描。
- 虚拟玩家现场提供可见点击落点及 WASD 方向候选；按键通过 InputEventKey 在物理帧边界真实按下，累计 0.35 模拟秒后释放，保留原碰撞、锁定与房间揭示，不预查未知落点房间或直接设置位置。按键计时采用可取消回调，释放前不选下一动作；记录按下/释放及实际位移，退出时取消回调并释放残留按键。停止后锁定现场，等待尚未完成的一秒不操作或演出 drain 返回，再留 0.15 秒清理输入缓冲并释放节点；清理时间不计入 stop 游玩测量，停止后的 drain 不再读取已关闭会话。仅提供可见点击落点不能覆盖跨房间，不能据这种驱动的选择数宣称跨区调查覆盖。
- 操控 JSON 服从 `WorldScriptDocument` 的 snake_case 读取约定：`label_contains`、`argument_weight`、`information_weight` 必须经真实编译/运行时往返验证，不能仅分别验证 Python 字典和手工 C# 对象。
- 键盘候选位移由当前身体速度乘以 0.35 模拟秒给出，输入从下一物理帧边界开始按模拟时间累计，按下和释放均刷新输入缓冲，不改全局累积输入设置；不再在加速测试中保持固定 350ms 墙钟；不得补跑物理帧、修改身体位置或单独推进游戏钟。1 倍与 4 倍的中性输入测试允许一个物理步误差，分别验证真实位移、碰撞和停止释放。

- 等待输入只有在后续真实公开台词、文字或完成状态改变时，才通过 Core 既有经历接口重开本次等待的尝试；无变化继续累计成本，其他输入和空间经历不清空。此合同属于玩家端口的公开结果适配，不代表全体 NPC 自动接入新的语义目标机制。

- 作答前提的中性回归使用 `PlayerControllerCheck/ScopedDeferredAnswerCases.cs`：先真实选择 `purpose=argument` 再暂退，验证只记住该作答方法、同入口检视/试验仍开放，相关完整支撑后重开。旧的整入口抑制夹具不再作为当前合同入口，失败记录仍保留。

- 现场移动的 `known_ground` 经 `GameEngine → PlayerFieldOffer → PublicPlayerCandidate.KnownGround` 闭合传递，参与候选身份。只由当前视野或先前实际看到的可通行格得到，不查询未知几何；普通已见落点不获未尝试/未走地面奖励，frontier 的未知边界价值与有依据的 approach 返回仍保留。位置、执行回执及碰壁学习不因此改变；省略元数据的兼容调用继续按未知处理。

- 轨迹审查以用户明确的实际行为标准判定：第一章不得有不符合正常人操作的行为或无意义低效率的重复查看。构建和中性机制检查只能定位实现错误，不作为设计/行为合理的证明；逐段记录重复前的认知、真实执行及新结果。
- 坐席按实际看到的来源菜单记忆未做方法；没有副作用且仅跳转END的对白退出由通用对白运行时标记close并经宿主透传，不靠文案识别。退出无新鲜奖励；同一已执行方法无自动第二次求知奖励。修改假设不产生调查信息，有已获知证明的同组假设不因未尝试偏好而被无依据选项替换。
- FullTest绘制采集记录render_wait_start/complete；等待使用可取消的过程帧轮询，3秒墙钟未获真实frame_post_draw则单独以render_capture_timeout停止，不把未执行的输入计为决策或寻路卡住，不以旧截图冒充新渲染。

- FullTest 每秒墙钟采集实际过程帧率、帧间隔 P95/最大值及引擎监视器；GameEngine 分别记录世界调度、认知快照和坐席决策耗时，AgentPilot 记录观察刷新与按时间缩放的阅读/等待实耗，截图记录同步采集耗时。阶段归属是帧边界采样，process/physics 监视器是采样瞬时值，不作为精确 CPU 分摊。图表从本轮原始轨迹生成，等待与计算分开，不以加速诊断当盲玩验收。
- StrategyPolicy.Bind 对语义相同的现有规则不重复写入内省历史；规则、条件、权重、接受状态、时效或公开说明改变时正常观察。StrategyAssessmentCache 仅在同一会话、同一主体与同一认知版本复用派生评分，任何认知或行动反馈写入使缓存失效；不保存到存档，不读取其他主体或权威状态。现场仍逐物理步调度，不靠降低物理更新频率掩盖卡顿。
- 宿主 GameEngine 就绪时经 GameSession.PrepareAsync 后台准备章节只读案件模型；模型仍由原有全组合独立重演计算，Lazy 保证只发布一份完整结果。后台任务不访问 Godot 对象或活会话，也不向人物写入预测/答案；异常返回主线程报告。同步模型读取入口保留兼容，若调用早于准备完成仍会等待，性能报告须明确这一边界。
- 同一表单内设置未知假设后先提交试验，不继续无依据切换同组；新获知证明允许纠正，提交或取消后开始新的尝试。此限制只管理已公开的假设选项，不注入正确答案。
