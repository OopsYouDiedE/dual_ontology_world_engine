## 第四部分：系统与界面

### 1. Core 映射

| 设计概念 | Core 实现 | 归属 |
| :--- | :--- | :--- |
| 客观世界 | `Essence` + `Property` | 游戏层定义对象 |
| 人物 | `SoulEssence` 与各自 `ProjectionWorld` | — |
| 感知与传播 | `ObservationInput` 经 `Observation.Observe`，Source 区分 Perception／Testimony／Recall／Inference／Corruption | 声路、视线、转述折扣与走样由游戏层计算 |
| 习惯、规则与欲望 | `Rule`（Condition／Effect／Mode／Weight／Acceptance）写入认知；欲望以相关规则权重表示 | 内容属游戏层 |
| 规则接受 | 对 `Rule.Acceptance` 的判定（信任、背书、欲望相合、利益威胁、亲见、成见） | 判定公式属游戏层；Core 只做必要的通用扩展 |
| 请求与信任 | `Effect.Say` + Request；`SocialSystem.Evaluate` | — |
| 行动 | `ActionRuntime` 准备后提交；`SimulationSession` + `CTBSystem`；同一身体不能同时做两件事 | — |
| 言灵 | 言灵词句编译为 `Ast` 规则 | 词汇与语法属游戏层 |
| 重演与夜间推演 | 重演每次新建独立世界；夜间推演在现实世界上执行并提交 | — |

Core 不出现专案名词。

#### Python 剧本编译与宿主边界（2026-10-05 重构）

**显示绑定与事件边界（2026-10-05 用户裁定）。** Godot 实现具体显示绑定，通过类型化事件与纯 C# 权威运行时交互。输入为交互意图、候选请求、选择提交、宿主空间核验以及表现完成；输出为可用候选、已选行动、语义台词及世界变化结果。人物策略、条件判断、状态提交与章节继承归核心侧；对话框/证物卡选择、节点绑定、字体颜色、镜头、动画和音效播放归 Godot。Godot 不提交任意状态修改，不选 NPC 答案；核心不引用 Godot 类型或节点。事件需区分行动结果与表现确认，按关联标识防止重复/过期输入；无画面驱动应能使用同一接口。此处“核心侧”包含无 Godot 依赖的游戏脚本运行时，独立 Core 世界引擎仍不引入章节或 UI 专属类型。

**扁平候选与对白块（2026-10-05 用户裁定）。** 对话的基本单位是独立的候选行动和候选反应，不是由作者嵌套指定下一句的对白树。玩家从当前可用候选中选择；NPC 在有限的适用反应中，依据自身主观世界的已接受策略、欲望和代价，调用 Core 决策机制选择。条件决定适用性，策略决定偏好，两者不能混为作者写死的分支。人物条件读取其已知/相信的投影，不能偷看客观实体状态。没有适用反应时应明确返回无反应，不强行执行列表第一项。

候选台词使用三反引号包围的剧本块，直接写人物与正文；块外保留可读的声明与逻辑。这是 Python 语法加对白块的作者语言，不能再宣称文件是标准 Python。前端处理对白块时保留源位置，再解析受限 AST。台词、候选身份与执行效果绑定，选择提交时重新核验且不得重复结算。候选反应保持平铺、可独立增加，不能用候选内的 if/else 指定 NPC 下一句来绕开策略。场景物件中参与交互/因果模拟者绑定 Core 客观实体或属性/事件；纯视觉装饰仍由宿主处理，证物卡只是已有证据与主观认知的界面表现。

**章节组合与继承（用户追加裁定）。** 脚本按章节拆分，后输入章节显式声明前置章节并扩展同一个世界会话。区分定义继承与运行状态延续：前者继承世界设定、实体类型、属性定义、人物初始模型和通用规则；后者保留玩家已经造成的物件变化、持有关系、人物认知/信任/欲望、已触发事件与任务进度。第二章不能重新执行第一章初始化，不能用初始默认值覆盖已发生的变化。

- 定义采用稳定身份和章节命名空间；名称只在编译、装载和存档边界解析，运行态沿用对象引用。后章可新增实体与交互，扩展既有定义必须显式声明；同名重定义、类型冲突、前置缺失和依赖循环均报错，不能静默采用后者。
- 同一会话顺序载入时保留既有人物、物件和认知对象身份。世界持久状态跨章存在；章节局部状态与一次对话临时状态具有独立作用域。章节退出只注销声明为本章有效的触发器，不清空世界；持久规则继续生效。
- 装载前先验证整个章节及引用，再提交新增内容；失败不得残留半个章节。重复载入已加载章节不得重复创建对象或发放奖励。章起始效果在明确激活时执行一次；装载内容本身不等于自动推进剧情。
- 后章修改初始模板不追溯改写旧人物；要改变当前世界必须使用显式效果并遵守行动提交与观察通路。剧情上销毁的物件不得因跨章重新声明而复活。缺少前章运行记录时可以显式选择新游戏初始化或测试夹具，但不能冒充真实继承。
- 存档最终需保留内容版本、已激活章节、世界状态、人物主观世界、触发记录及必要的时序/随机状态；不同进程恢复后重建引用关系。同一会话继承与跨进程存档恢复分别验收，前者通过不代表后者完成。

**《极乐迪斯科》参考能力与本作边界。** 官方早期开发日志记录了 Articy 编写、导出到游戏、逐对白处理可见性/效果/能力检定，以及供作者游玩验证的工具（[ZA/UM 开发日志](https://discoelysium.com/devblog/2016/02/26/peculiarities-eastern-european-game-development)）。Articy 官方文档说明对象模板、全局变量、条件/指令及模拟预览（[脚本说明](https://articy.com/help/adx/Scripting_in_articy.html)）。这些资料支撑作者工作流参考，不能据此宣称《极乐迪斯科》内部使用本作的章节继承或自主世界模拟架构。

本作脚本能力目标覆盖：实体模板与带类型属性；带命名空间的状态；条件选项及失败分支；可复用对话枢纽、访问记录与一次性台词；内心声音和主动/被动检定（难度、修正来源、重试条件与已结算结果）；任务阶段与事件；物品/关系/状态效果；跨章定义和状态延续；带源码位置的作者预览与执行解释。检定和内心声音是通用可选能力，不自动替换本作认知推演玩法。章继承、自主决策及现实/认知分离是本作自行设计并验证的部分。以上为能力目标，不表示现有代码全部支持。

**2026-10-05 用户追加裁定：世界定义与模拟脚本。** 最终脚本层必须覆盖房间、所有场景物体的交互属性、人物性格、身体与精神状态，以及额外的剧情驱动。现有 `line/condition/effect` 对白构造器只是迁移阶段实现，不能作为完成此要求的验收依据。以下是目标契约，尚不表示当前编译器已经支持。

- 作者使用人类可读的 Python 语法：命名对象与属性、命名参数、普通 `if/elif/else`、交互和事件函数。支持中文标识符与文本；不得要求作者手写 AST、嵌套表达式构造器、指令操作码或 JSON。编译器解析明确的 Python 语法子集，条件在 C# 运行时求值；不能在编译时执行剧情函数来决定分支。不支持的语法报源文件、行列与可理解的原因。
- 世界声明包含房间、连接、物件、人物、初始状态、资源与空间布局引用。物体的检视、使用、组合、交谈等交互，其前置条件、动作代价、结果与反馈都可在脚本定义。既有通用能力范围内新增房间、物体或人物无需修改章节 C# 或 GDScript；新的底层物理/模拟能力仍可由引擎扩展。
- 性格必须有模拟语义：通过欲望权重、主观规则、信任与行动偏好影响真实决策；描述文本不能代替模型。身体与精神状态可以影响行动条件和代价。客观对象状态、人物知道或相信的内容、作者控制的剧情进度必须区分；人物认知只能经观察、转述等既有观察通路改变，人物行动依自身认知决策，不得直接读取全知状态。
- 额外剧情驱动可订阅进入房间、完成行动、观察所得、状态变化及逻辑时间推进，声明触发条件、一次性或重复策略、优先级和效果。由 C# 在行动提交边界调度；重复确认不能重复结算，循环触发必须可诊断。预设剧情与自主模拟共用权威状态，不能各维护一套结果。
- 编译生成类型化世界定义与行为中间表示；加载阶段解析名称并绑定真实对象引用，运行时沿用核心的类型化节点与引用。Godot 消费布局与表现请求，负责房内几何、碰撞和实时动画，不执行剧情条件。无画面的 C# 会话必须能构建同一世界、输入行动并推进模拟。
- 验收必须包括仅改脚本即可新增房间/物件/人物/事件，以及同一交互因人物性格、认知和状态不同产生可解释的不同结果；自动测试与独立实机盲玩分别记录。第一章 Python 内容迁移与第二章 `.world` 接入分别验收；任一章节接入不等于上述通用新增能力已经全部达成。

两章采用同一种 `.story` 剧本语言（草案见 `artifacts/unified-story-script/spec-draft.md`）。第一章正式源位于 `godot_csharp/scenes/Awakening/Script/chapter_01.story`，由 `story_chapter1.py` 读取，规范库与编译器位于 `godot_csharp/tools/engine_spec.py`、`compile_story.py`。编译器在构建时运行，游戏运行时仅使用生成的纯 C# 剧本数据及 Godot 布局 JSON，不引入 Python 运行时。对白、分支、证物事实、协助说明和热点声明由同一源维护；生成文件不得手工另改。编译器验证分支引用与支持的表达式，遇到不支持的语法明确失败，不默默丢弃。

交互协议包含说话人、情绪、正文、语音标识、带可用性与锁定提示的选项、呈现形态与舞台命令。协议和章节运行时属于游戏层的无 Godot 依赖 C# 程序集；独立 `csharp/` Core 继续只负责双重本体、观察、决策与行动结算。剧情条件与状态变更由 C# 权威处理，Godot 负责实际位置核验、移动、碰撞、镜头、音效及 UI，并将舞台动作完成反馈给剧情运行时。

**2026-10-06 第二章迁移实现边界。** 唯一正式作者源为 `godot_csharp/scenes/Awakening/Script/chapter_01.story`；生活翼正文及 `续篇 chapter2` 的始业与后续案件共用人物、地点、词汇。`compile_story.py` 一次生成现有两个宿主适配器所需内容；`story_script.py --section chapter2` 也可独立编译续篇作用域，前置阶段标识仍为 `awakening`。作者文件合并不等于两个宿主已合为一个，也不构成人物动机或游戏体验验收（2026-10-07 起两章宿主已合为一个，见本部分 §2）。脚本声明房间、现有 Core 人物与物件的稳定身份、人物画像、场景交互点、扁平候选和剧情场面。现有 C# `Game` 仍计算职业行动、感知、案件重演与结局；章节交互会话消费作者数据并发布语义事件，Godot 宿主绑定地图与 UI。第一章退出时交接运行中的 `WorldRuntime`、`SimulationSession`、四人的身体/灵魂/投影及章节进度，第二章不伪造独立继承。跨进程恢复以及仅改脚本即可新增任意房间、人物、底层交互类型仍属后续通用能力验收，不能由此次现有实体绑定推断已经实现。迁移后的构建、流程回归及实机测试由 Gemini 执行；未得结果前不写“验证通过”。宿主瘦身以消除重复业务为准，不为达到行数目标把同一业务换个 GDScript 文件存放。

### 2. 所有章节共用的界面与事件接口（2026-10-04 统一；2026-10-07 合为一套，用户要求）

**用户要求（2026-10-07）**：合并第一、二章的界面；主代码只负责接收和转发来自引擎的事件、接收界面回传的事件；界面从第一章到后续任何一章完全一致。界面按模块分门别类，组件一律 `@tool`，在编辑器里改值即可预览。

**分层（硬约束）**

| 层 | 位置 | 职责 | 不做 |
| :--- | :--- | :--- | :--- |
| 章节会话（纯 C#） | `IChapterSession`（`Shared/Story/ChapterProtocol.cs`）；第一章 `Awakening/Script/Chapter1Session*.cs`，第二章 `Chapter2/Prototype/Chapter2InteractionSession.cs` 与 `Chapter2SessionView.cs` | 剧情、条件、文字、流程编排（相遇、触物、请人比对、声路、答辩、对质、议事、日常）、快照 | 不引用 Godot；不读宿主坐标，只读输入里现场报告的几何事实 |
| 桥 | `Shared/ChapterEngine.cs` | `Open(章节)`、`Dispatch`、`Snapshot`；Variant 转换；写 `save` 事件的存档 | 不含章节规则 |
| 主代码 | `Shared/ChapterHost.gd`（每章同一份） | 现场与界面的事件附上几何后转给章节；章节事件转给现场或界面；快照交给两者；章末换场景 | 不解释任何事件内容 |
| 现场 | `Shared/Field/ChapterField.gd` + 地图适配器（第一章 `Awakening/AwakeningMap.gd`，学园 `Chapter2/Field/CampusMapView.gd`） | 行走、点击走近、悬停与最近交互点、视线、人物显示、走位与镜头、日常落脚、昨夜回放（地图上） | 不决定谁去哪里、说什么 |
| 界面 | `Shared/UI/ChapterUI.tscn`；组件在 `Shared/UI/{HUD,Dialogue,Menu,Card,Form,Notebook,Manuscript,CampusMap,Ending,Field,Common}/`，总览页 `Workbench/` | 抬头、对白、菜单、证物卡、表单、笔记本、重演手稿、学园图、章末卡、心声、标题闪现、淡入淡出 | 不自备任何章节的文字、表格或判断 |

章节差异只在入口场景里设置：`Awakening/Chapter1.tscn` 与 `Chapter2/Chapter2.tscn` 是同一个主代码、同一个界面，只换 `chapter_id`、地图适配器与起始地图。新章节只需新增会话实现与（如有）地图适配器。

**输入（现场与界面 → 章节）**：`begin`；`presented{presentation_id, ok?, reason?}`（台词读完、走位或镜头完成、回放看完）；`select{presentation_id, choice_id, token}`；`form{presentation_id, action, values}`；`interact{map, site, target}`；`card{card_id}`（从笔记本或手稿重开卡片）；`furniture{map, nodes, texts}`；`move{map}`；`action{act}`（多日章节的时段行动，T 为 `stay`）；`replay{vars, skip}`、`replay_vars{vars}`；`opened{panel}`；`tick{delta}`（快照 `realtime` 为真时每物理帧一次）；`daily{who, serial, status, room}`。每个输入附 `context`：`field_active`、界面弹窗种类，以及每个在场人物的 `room`、`entered`（脚下实际室内地面）、`room_name`、`visible`、`moving`、与侦探的 `distance`、`near_sites`。章节据此判断“在场”“当面”“够得着”，不另取坐标。

**输出（章节 → 现场或界面）**：`lines{presentation_id, lines[{speaker, speaker_name, mood, text, voice_clip, line_id}], choices?}`；`candidates{title, prompt, options}`；`card{title, picture, facts, source, intro, notes, items, table, options[group=inline?]}`；`form{title, prompt, header, layout: stack|tabs, fields, summary, actions}`；`ending{title, outcome, rows, actions}`；`aside`、`title`、`music`、`sound`、`duck`、`close`、`manuscript`、`error`、`save`、`state{snapshot}`；交给现场的 `scene{map, spawn, mode}`、`stage{op: walk|place|pose|face|sit|stand|halt|camera|prop|group|cancel_click|daily_go|daily_work|daily_release}`、`replay_play{mode: stage|campus}`。带 `presentation_id` 的输出必须回执；同一时刻只有一个阻塞的界面呈现，后台作业等它结束。

**快照**：`chapter{title, window_title}`、`stage`、`ended`、`field_enabled`、`player_control`、`realtime`、`hud{title, time, keys}`、`controls{notebook, manuscript, campus, wait}`、`access`、`sites{id: title, enabled, new}`、`people[{key, name, title, map, sprite, present}]`、`pinned`、`cast`、`notebook{sections}`、`replay{open, title, hint, questions, vars, last_run_vars, result}`、`campus{map, known}`、`daypart`。

**界面行为（两章完全一致）**
- 现场：点击移动与交互，WASD 移动，E、空格或 Enter 交互；可交互的物件与人统一高亮，悬停显示名称；普通家具一句心声。
- 证物卡：局部像素图 + 事实 + 来源；可直接试验、请在场的人辨认或“请他看看”；任意键返回。
- 对白：共享对白框与立绘（十六人皆有立绘）；两章均消费 Core 发出的交互数据包，第二章对白与候选由 `.world` 编译产物供给，人物回应按其认知与策略选取，共用现有呈现组件。
- 笔记本（Tab）：证物按地点分组；读过的规矩（谁立的、贴在哪）；人（侦探亲眼见过的迹象与来源，可能错）；回忆所得。
- 重演手稿（R）：所有章节共用 `Shared/UI/Manuscript/ReplayManuscript`，问题、选项、对照与证物名全由快照 `replay` 给出；左页变量，右页对照，“重演”后回放（单一地图的章节回放在实景上，多地点的一夜回放在学园手稿图上）。
- 对质：一问一答；题目与所选的陈述、出示固定在顶部，“陈述”“出示”两列各选一项后“作答”。
- 多日章节另有：顶栏显示“第 N 日 · 时段”；时段交替时短暂转场，众人换到各自的去处；夜里在床前或守夜点选择睡下或守着；议事时选择站在哪一方。这些由快照与事件决定，界面不分章节。
- 挂在墙上的交互点（隔离门、门框接头）其坐标落在墙格：从它朝向的房间看得见（2026-10-07 修正：此前隔离门在门厅里看不见、点不到）。

