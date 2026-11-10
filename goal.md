# 双本体世界引擎：项目完整结构与架构评审资料

对象：`dual_ontology_world_reconstruct`。整理日期：2026-11-10。

这是一份可单独交给 Claude 的结构说明，包含当前实现、目标拆分、关闭语义、代码索引和验收边界。阅读本文件不需要打开其他设计文件。文中路径均相对 reconstruct 根目录。它不是全部源码副本；目录、接口和文件映射不能代替实际源码语义审查。

## 1. 请先理解项目目标

这是 Godot 3D 生活模拟与人物认知/推理游戏，使用纯 C# Core、GDScript 场景/UI 和 DV 内容声明。

用户希望先建立模拟人生类生活基础：人物有需求、性格、情绪，发现可用物件，自主选择或接受玩家命令，真实移动并使用物件，产生需求/关系/资源效果。开启可选能力后，再加入本项目的独立信念、证据传播、说服、证明推演、反事实做局、侵蚀和案件叙事。

模拟人生只是玩法与通用需求效用模式参照，不是要复制 EA 内部源码，也不要求首期做齐全部经济、家庭、职业、生育和年龄内容。项目中的 `Sim4Planner` 是本项目类名，不证明它就是《模拟人生4》的实际 AI 架构。

### 状态标记

- **当前代码事实**：根据已有源码/工程配置进行的静态核对。
- **目标设计**：模块边界、能力预设、依赖与验收条件；不代表已经实现。
- **实现缺口**：当前证据不足、耦合未拆或未具备完整功能。
- 本项目尚未实现本文件设计的完整能力开关，也未通过“关闭全部特色后仍可生活”的游戏验收。报告工具通过检查不能当作玩法已通过。

## 2. 技术栈与入口：当前代码事实

| 项目 | 当前内容 |
| --- | --- |
| 正式 Godot 入口 | `scenes/main/Main.tscn`，专属 `Main.gd` |
| 引擎声明 | `project.godot` 声明 Godot 4.7 / C# |
| .NET 工程 | 根工程使用 `Godot.NET.Sdk/4.7.2`，目标 `net8.0` |
| Core | 纯 C#，不应引用 Godot 命名空间；当前八个工程 |
| 场景与 UI | `.tscn` + 专属 `.gd`；少量场景辅助 C# |
| 内容 | `.dv` 声明人物、规则、物品、认知初值与初始布置 |
| 宿主 | Godot 和 CLI 共用当前 RuntimeSession 核心；另有旧 DvSession 兼容实现 |
| 工具 | Python、GDScript、HTML/JS，涵盖建造、内容、美术资产、调优和报告 |
| 全局加载 | GameLocale、GameAudio、GameSettings、GameMcp |

### 当前正式主链

```text
Main.gd 新局
  → SessionBridge.Open
  → CharacterSessionDefinition.Create
  → RuntimeSession
      ├─ World / FactStore
      ├─ Actors / ActorClient / 人物认知
      ├─ SimulationClock / WorldRules
      ├─ DecisionScheduler / DriveCalculator / 规划
      ├─ Reservations / 房间图 / 空间
      └─ 行动、对话、推演与策略协调

UI / 场景公开操作
  → 会话 Submit
  → 决策与执行任务
  → Godot 角色移动、对齐、姿态与工作
  → SessionBridge.Complete / RuntimeSession.HostCompleted
  → Core 客观效果结算
  → 授权观察 / UI 呈现

NativeCli → RuntimeSession（另一正式宿主）
旧 DvSession / DV*Kernel 兼容函数（不可直接画成每条都在当前主链执行）
```

`HostCompleted` 与旧 `F_dvs_session_complete_commit` 不是已证实的一次直接调用。动态调用、Hook、兼容函数是否生效，需要实际调用链证据。

### 状态与时间所有权

- 客观事实和人物所知分别保存。人物只能经正常感知、通信或合法推演获得信息。
- 预测需求收益不等于真实收益。开始动作、播放动画或 UI 进度结束不能替代真实完成结算。
- Core 负责逻辑事实、规则、时间、认知、证明和保存状态；Godot 负责真实物理执行与表现。
- 游戏时间与物理移动时间区分；游戏加速不能简单把角色实际步速一起倍增。
- 同一动作以执行 ID / 代际关联到达、工作、接触、完成和取消，迟到回调不能结算新任务。

## 3. 三种结构不要混淆

1. **物理目录与编译工程**：当前源码真正位于哪里、引用什么工程。
2. **旧 16 个技术归属单元**：用于完整代码归属、技术边界和检索。
3. **当前功能设计**：5 UI + 8 场景；11 生活 Core + 7 特色 Core。一个协调脚本可能暂时涉及多个功能系统，不表示这些系统应合并。

本轮拆分是设计分析，没有按新系统搬移源码或增加程序集。

## 4. 项目协作与架构约束

- Core 和 DV 解析器用 C#；剧情、人物、认知初值、世界规则、物品与初始布置用 DV，不在宿主脚本重复写一份逻辑。
- 内容热重载保留事实、运行状态和认知证据；需要重新初始化的变更明确要求新局。
- 旧章节通过 C# 兼容前端逐步迁入 DV，不恢复 DV 自举编译器或固定点构建。
- 场景目标组织为 `scenes/main/`、`scenes/UIpanel/`、`scenes/components/`；场景/脚本严格专属 1:1 配对。当前还存在 `rooms/`、`prefabs/` 和工具场景，这里列真实目录，不声称规范已全量落实。
- 组件通过 Main 会话公开接口提交操作，UI 不读取宿主私有字段。
- 建筑工具目标位于 `tools/building/`，MCP 建筑工具目标位于 `mcp/building/`；当前另有 `tools/godot_architect/`、网页编辑器等历史/并存入口，不能因目标路径就认定它们已迁移。
- 工具输出先作为候选，明确发布后才进入正式游戏资源。初始布局不能在 `.tscn`、DV 和宿主脚本中各自独立维护。
- 构建、缓存、日志和报告放生成目录，不提交二进制/缓存。清理逐项按具体路径，禁止递归删除。
- 界面、游戏与模型工作分别控制改动范围。公共接口变更同步架构文档。
- 设计文件只写当前设计；实施、迁移、测试记录集中在 `docs/33 实施与验收.md`。

## 5. 已核对的主要实现缺口

| 范围 | 当前事实 / 风险 |
| --- | --- |
| 模块开关 | 没有本设计完整的能力解析、依赖闭包和内容裁剪入口，不能把设计预设当已有配置 |
| 基础与高级认知 | DecisionScheduler 的选择/提交依赖 ActorClient.Recall、观察/更新状态，并混合日常候选、TaskPlanner 与 GoalPlanningAdapter |
| 会话协调 | RuntimeSession 同时持有世界、人物、行动、空间、对话、推演、策略和检查点，多职责集中 |
| 工程耦合 | 根 Godot 工程引用 CLI 可执行工程；CLI 又显式链接 tools/story/ScenarioAuthoring.cs |
| 旧新兼容 | 类型化 RuntimeSession 与 object/list 形态 DV*Kernel 并存；部分 Hook 是进程级静态注册 |
| 接口空实现 | SessionBridge 中存在空体或固定返回方法，例如记录/证据封存相关接口；接口存在不等于能力完成 |
| DV 编译名称 | DvCompiler 的 C# 导出是内容/Schema/Hash/RawJson 静态内容导出，不能直接称为完整业务 AOT 编译器 |
| 推演 UI | DeductionGUI 已有规则/线索列表、勾选与提交，不等于完整图形化推演工作台 |
| 暂停书 UI | BookUI 已有页面、SubViewport 与输入映射；Main 禁用现场处理并隐藏场景，尚不能据此证明 Core、队列和声音全部暂停 |
| 音频 | 已有双音乐轨淡化、音效池和总线；GameAudio 使用非空间播放器，不能认定空间声/隔墙声学完成 |
| 建造 | 编辑器插件已有撤销放置；LiveBuilderOverlay 有候选/旋转/ghost，未确认完整运行时保存、撤销和发布闭环 |
| 角色验收 | 旧 MovementRegression 另实例化 Main 并读取私有成员，不能直接作为当前正式入口的公开操作验收实现 |
| 生活品类 | 物资/持有、目标/角色初值有基础，但完整通用经济、关系网络、技能职业、家庭和生命历程尚未证实齐备 |
| 旧门禁 | 部分 ontology/cognition/drives 标记 excluded，部分作者检查 skipped；不能沿用为当前验收通过 |

以上是结构审查关注点，不是所有函数都已经完成动态语义证明。

## 6. 如何验证功能

按二级职责、单系统、组合模式三层验收。独立验收允许依赖已验收模块，不要求建立临时工程。

1. 生活正式新局：需求变化、自主吃喝睡、手动命令/取消、多人争物资/座位、日常交谈、保存继续。
2. 逐项开启特色：证据隔离、消息真实送达、个人采纳、证明拒绝、预测不写现实、侵蚀后果与案件进展。
3. 组合验证：非法依赖拒绝、禁用模块无注册/定时/候选/评分/结算贡献、内容仍有生活必需路线。

CLI 验证逻辑、规则、事务、认知；Godot 验证真实移动、碰撞、姿态、接触、画面和声音。CLI 成功不能证明角色真的走到或坐下。

自动验收只用 `dual-ontology-auto.CliMCP/GodotMCP`，设置时长或截止时间，期限包含构建与启动，全程连接。AI/人工按 `dual-ontology-input` 的 `on → step → observe → close`，基于最新观察串行操作，结束关闭。从正式 Main 和正式新局开始。

禁止回放冒烟、状态注入、开发传送、伪造回执、临时测试工程、一次性玩法测试脚本和合成端点。没有自然到达的条件、缺内容或缺观察字段，记录为未覆盖。

每项保存系统/用例 ID、版本与内容哈希、seed、run、backend、角色/物件/执行 ID、前观察、操作、后观察、预期、实测、阈值来源、原始回执、截图/声音证据及覆盖限制。结果只分通过、失败、未覆盖。

## 7. 希望 Claude 重点评审的问题

请基于下面的实际工程结构和目标功能设计，给出独立架构意见：

1. 生活基础是否足够构成稳定的生活模拟闭环？哪些职责遗漏、过度拆分或命名不清？
2. 特色能力与基础能力边界是否合理？尤其是轻量生活记忆与证据认知、普通任务规划与反事实做局、日常社交与命题通信。
3. 基础不依赖特色实现的目标，在现有 ActorClient/DecisionScheduler/RuntimeSession 结构下应如何渐进达成？
4. 哪些能力必须有依赖，哪些可以独立关闭？关闭后有哪些隐藏调用、世界规则、存档或生活内容残留风险？
5. 能力配置应如何进入 DV 内容、新局装配和存档版本？哪些情况应拒绝启动，哪些允许显式裁剪？
6. 是否需要新的程序集？若需要，优先拆哪些契约、状态和宿主适配，如何解除 Godot → CLI Exe 依赖？
7. 先实现哪一个最小纵向闭环，才能尽早证明“无特色仍能生活”？验收需要哪些真实可观察字段？
8. 请区分必须修改、可以后做和需要更多源码才能判断，不把目标设计当作现有实现。

以下章节附全部系统定义、编译依赖和结构索引。


## 8. UI 与场景：目标功能设计


以下共 13 个一级系统、61 个二级验收职责。源码映射仅表示现有承载位置，不表示代码已按系统隔离。


### UI 系统


#### UI01 脑内推演 UI 系统


- 边界：展示本人已知证据、规则和结论选择并提交意图；不在 GDScript 再实现证明搜索或读取 NPC 私有心智。

- 输入：观察者授权的推演预览、证据 ID、规则描述与既有执行结果

- 输出：action_id/evidence_ids 提交意图，关闭意图与 UI 状态

- 依赖：A01、A02

- 当前映射：已有 DeductionGUI 的规则/线索列表、勾选与提交；Core 证明校验是外部依赖。图形化推演工作台仍属设计扩展，不能据简单浮层认定完成。

- 代码：`scenes/UIpanel/GUI/DeductionGUI.gd`；`core/reasoning/CaseProofChecker.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| UI01.1 | 证据浏览与筛选 | 只显示当前观察者获准知道的线索，来源、确认状态与 ID 对应原始证据。 |

| UI01.2 | 规则、前提与目标呈现 | 所展示规则、必要前提与当前目标匹配正式推演动作，不把未知当假。 |

| UI01.3 | 推演操作区 | 选择/连接/撤回只改变本地草稿，未提交时不修改认知与世界。列表版与图形版可共用该契约。 |

| UI01.4 | 提交与结果反馈 | 同一提交最多执行一次；缺前提、过期证据、成功和失败均使用 Core 原因反馈。 |



#### UI02 GUI／现场 HUD 系统


- 边界：现场持续信息、目标动作与交互反馈；推演由 UI01 负责，暂停书本由 UI05 负责。

- 输入：只读 interface 观察、目标屏幕锚点、正式任务和已送达对话/说服结果

- 输出：选择、动作、取消、速度/自主控制等公开操作意图

- 依赖：A01、A02、UI01

- 当前映射：已有 FieldGUI 组合、时间/任务/动作列表、引导及说服面板；当前由 FieldGUI 挂载推演浮层，功能归属可独立于场景父子关系。

- 代码：`scenes/UIpanel/GUI/FieldGUI.gd`；`scenes/UIpanel/GUI/ClockRoomGUI.gd`；`scenes/UIpanel/GUI/TaskProgressGUI.gd`；`scenes/UIpanel/GUI/ActionChoiceGUI.gd`；`scenes/UIpanel/GUI/GuidanceGUI.gd`；`scenes/UIpanel/GUI/PersuasionGUI.gd`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| UI02.1 | 时间、地点与运行状态 | 显示正式世界时间、房间和暂停/速度，不自行累计一套游戏时钟。 |

| UI02.2 | 任务进度与取消 | 行走、到达后工作、完成和取消分别呈现，不以 UI 进度条结束触发逻辑成功。 |

| UI02.3 | 目标选择与动作菜单 | 动作来自当前合法选项；目标失效/遮挡或模态打开时不保留可点击的过期菜单。 |

| UI02.4 | 对话、说服与交互反馈 | 只呈现已送达内容及公开原因类别，不展示 NPC 私有评分和未发生的判定。 |

| UI02.5 | 引导、需求与控制提示 | 提示指向真实可执行操作，不代替玩家执行、不暗示角色未知剧情。 |



#### UI03 音效与音乐系统（全项目共享）


- 边界：播放、混音、曲目与环境声呈现；角色听觉传播、内容辨认与证据归 Core 感知。归 UI 组管理不表示只服务按钮。

- 输入：UI/场景公开声音事件、正式音乐上下文、空间位置、音量/暂停设置

- 输出：声音播放与停止、总线参数、音乐上下文 token 和音频诊断

- 依赖：A01、A02

- 当前映射：已有静态 Sound 入口、GameAudio 双轨淡化和音效池、MusicContext 与总线。现有 GameAudio 使用非空间 AudioStreamPlayer，不能据此宣称完整世界空间声/遮挡已完成。

- 代码：`scenes/components/shared/Sound.gd`；`scenes/components/shared/GameAudio.gd`；`scenes/components/shared/MusicContext.gd`；`assets/audio/default_bus_layout.tres`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| UI03.1 | UI 操作音 | 点击、翻页、确认/错误反馈与实际操作一致，同一事件不重复播放。 |

| UI03.2 | 世界交互与空间音 | 脚步、门和家具声音绑定真实事件与位置；听者位置、声源清理和声道归属正确。 |

| UI03.3 | 配乐与环境声上下文 | 按正式上下文切曲、循环和淡化；旧 UI 回调不得打断新曲目，也不从隐藏剧情选曲。 |

| UI03.4 | 混音、并发与设置 | 总线音量、静音、并发池和优先级一致，关键反馈不被大量环境声吞掉。 |

| UI03.5 | 暂停、跨场景与退出 | 按策略保留菜单音/音乐并暂停世界声；隐藏节点不当作停止声音，退出释放播放器。 |



#### UI04 标题界面系统


- 边界：标题菜单、可继续状态和模式切换意图；3D 书房背景属于 SC08，共享音频属于 UI03。

- 输入：存档可用性、菜单状态、用户输入与书房交互命中

- 输出：new_game/continue/book/quit 等意图

- 依赖：A01、A02、UI03、SC08

- 当前映射：已有 TitleUI 和 Main 信号编排；独立验收标题状态，不重复创建另一套新局/存档服务。

- 代码：`scenes/UIpanel/TitleUI/TitleUI.gd`；`scenes/main/Main.gd`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| UI04.1 | 菜单和可用性 | 无有效存档时继续不可用；文字/禁用/悬停状态一致。 |

| UI04.2 | 标题输入与焦点 | 鼠标及配置支持的键盘操作不误触书本或背后世界，禁用项不触发。 |

| UI04.3 | 新局、继续与退出转场 | 重复点击不创建多会话，加载失败可反馈并回到可操作状态，退出关闭真实宿主。 |



#### UI05 游戏暂停／书 UI 系统


- 边界：暂停入口、书页导航、存档/设置页与书内输入；3D 书叶、镜头和纹理由 SC08 配合，实际存档由 A02/Core 执行。

- 输入：书页定义、公开保存槽、设置、返回模式与舞台坐标映射

- 输出：暂停/恢复请求、书页纹理、翻页请求及存取/设置意图

- 依赖：A01、A02、UI03、SC08

- 当前映射：已有 BookUI 的图录/存档/设置页、SubViewport 和 push_input；Main 当前通过禁用现场处理并隐藏现场切换至书房。需要验收 Core、物理、执行队列与音频的实际暂停范围，不能只看书已打开。

- 代码：`scenes/UIpanel/BookUI/BookUI.gd`；`scenes/UIpanel/SaveUI/SaveUI.gd`；`scenes/UIpanel/SettingUI/SettingUI.gd`；`scenes/main/Main.gd`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| UI05.1 | 打开、关闭与暂停请求 | 从现场与标题进入时返回语义不同；连续开关、加载失败和嵌套弹层不提前恢复现场。 |

| UI05.2 | 书页导航和内容页 | 标签、前后页、当前页和图录/存档/设置显示一致；空图录不宣称已有完整案卷内容。 |

| UI05.3 | 3D 书页输入映射 | 屏幕→书页 UV→SubViewport 坐标正确，翻页中锁输入，拖拽/滚轮/关闭不穿透到现场。 |

| UI05.4 | 存档与读档界面 | 只发合法槽位操作，成功/失败与宿主回执一致；UI 不自行序列化另一份世界。 |

| UI05.5 | 设置、语言与可读性 | 音量/语言/减少动态等设置实际生效，布局不截断，返回后状态正确。 |



### 场景 系统


#### SC01 建筑基本结构自动生成系统


- 边界：从规范化尺寸与结构声明生成墙、楼板、顶、楼梯/栏杆及洞口；不包含玩家编辑器交互或角色寻路执行。

- 输入：结构参数、局部坐标/尺度、材质引用与门窗洞口声明

- 输出：结构节点、网格、碰撞、包围盒和拓扑/门洞元数据

- 依赖：A03

- 当前映射：已有 @tool 参数化结构和延迟 rebuild。生成算法、尺寸约束与洞口一致性是自研；ArrayMesh/SurfaceTool 本身是引擎能力。

- 代码：`scenes/components/architecture/SmartWall3D.gd`；`scenes/components/architecture/SmartFloorZone3D.gd`；`scenes/components/architecture/SmartStair3D.gd`；`scenes/components/architecture/SmartRailing3D.gd`；`scenes/components/architecture/ExteriorRoof3D.gd`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| SC01.1 | 坐标、单位和参数校验 | 米制、轴向、端点、厚度、高度和楼层约束明确，非法参数明确拒绝/规范化且有说明。 |

| SC01.2 | 结构几何与重建 | 同一参数生成稳定结构，多次 rebuild 不积累旧网格/碰撞，修改只更新有关组件。 |

| SC01.3 | 门窗洞口及接缝 | 视觉洞口与实际碰撞开口一致，墙角/楼板/楼梯接缝满足净空。 |

| SC01.4 | 碰撞和导航源元数据 | 角色真正可走的面与声明一致；网格、碰撞和导航源变更版本能关联。 |

| SC01.5 | 材质、剖切和编辑器预览 | 编辑器预览与正式实例尺度一致，剖切不误删碰撞或改变世界状态。 |



#### SC02 家具与交互物件生成系统


- 边界：此处生成指原子资产的参数装配、智能预制件实例化与元数据绑定；原始模型制作/AI 生成属于制作流水线。

- 输入：预制件目录、变体、DV/实例 ID、材质和交互槽配置

- 输出：家具候选或正式实例、碰撞、交互锚点与动作/资源绑定

- 依赖：A03

- 当前映射：已有 PropEntity3D、智能预制件、碰撞生成、ReachTarget 与槽位标识。模型存在不等于站位、坐姿、交互和逻辑 ID 均已有效。

- 代码：`scenes/components/architecture/PropEntity3D.gd`；`scenes/components/characters/ReachTarget3D.gd`；`scenes/prefabs/`；`tools/building/`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| SC02.1 | 资产目录与实例装配 | ID/类别/变体可追溯，实例之间不意外共享可变资源。 |

| SC02.2 | 尺度、材质与碰撞 | 家具符合角色尺度；装饰/阻挡/可交互碰撞层正确，无重复碰撞。 |

| SC02.3 | 站位、触及点与座位 | 站位可达、朝向和接触点匹配；容量与逻辑资源槽一致。 |

| SC02.4 | 物件动作及逻辑绑定 | 抽屉/机关等可视动作有完成/取消语义；动画不自行修改 Core 事实。 |

| SC02.5 | 候选保存与发布校验 | 打包保留必要 owner/锚点/碰撞/标识，重载保持配置；未发布候选不进入正式新局。 |



#### SC03 可视化房间修建系统


- 边界：消费建筑与家具生成能力，负责编辑命令、选择/变换、候选保存和明确发布；不重新实现墙体/家具算法。编辑器与运行时为两个前端。

- 输入：编辑命令、结构参数、预制件和房间候选

- 输出：可撤销命令、候选房间数据、验证结果和发布请求

- 依赖：SC01、SC02、A03

- 当前映射：已有编辑器预制件放置及 EditorUndoRedoManager，F4 运行时候选放置/旋转。当前运行时脚本未展示完整保存、撤销和发布闭环，不能因注释提到 save 就视为已实现。

- 代码：`tools/godot_architect/ArchitectPlugin.gd`；`tools/godot_architect/ArchitectGizmo3D.gd`；`tools/godot_architect/PrefabPaletteDock.gd`；`tools/godot_architect/LiveBuilderOverlay.gd`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| SC03.1 | 选择、拾取与工具模式 | UI 输入不误落到世界放置；选择工具、墙体工具与家具工具互不串用。 |

| SC03.2 | 变换、吸附与预览 | 移动/旋转/尺寸吸附统一；ghost 不参与正式碰撞和逻辑注册。 |

| SC03.3 | 放置与房间合法性 | 检查重叠、门洞净空、楼层/边界、连通及槽位；有限座位校验不冒充完整场景合法性。 |

| SC03.4 | 命令历史与撤销重做 | 编辑器接入 EditorUndoRedoManager；运行时接入 UndoRedo 或统一命令栈，不调用编辑器 API。 |

| SC03.5 | 候选持久化与恢复 | 重启编辑流程仍能恢复候选及稳定实例 ID，参数与依赖不丢失。 |

| SC03.6 | 明确发布与 DV 一致性 | 发布后布局/场景/DV 绑定一致且可追溯；作者源明确，运行初始布置不在宿主脚本重复硬编码。 |



#### SC04 角色控制器系统


- 边界：把合法动作/玩家移动意图执行为真实物理位移和交互；控制器持有单一运动主权，不直接写剧情/认知事实。

- 输入：玩家意图、准备后的动作、目标槽位、路径和取消请求

- 输出：真实位置/速度、到达/阻断、工作开始/完成/取消与物理测量

- 依赖：A01、A02、SC06、SC07

- 当前映射：已有手动马达、动作队列、准备动作执行与跨房；ActorMotor 使用现有路径代码。无需为了采用官方文档而强制重写成 NavigationAgent。

- 代码：`scenes/components/characters/ActorMotor3D.gd`；`scenes/components/characters/ActorActionQueue.gd`；`scenes/components/characters/CharacterController3D.gd`；`scenes/components/characters/AutonomousActorExecutor.gd`；`scenes/components/characters/RoomTransition.gd`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| SC04.1 | 意图仲裁与动作队列 | 手动、点击和自主动作优先级明确，同一角色不同时被两个执行器移动。 |

| SC04.2 | 路径跟随与物理移动 | 寻路给路径，控制器在物理帧移动；墙、坡、台阶和动态阻挡有可观察结果。 |

| SC04.3 | 到达、转向与交互对齐 | 先实际到达/对齐/接触再开始工作或确认完成，不靠动画时长假定到位。 |

| SC04.4 | 工作、取消与失败恢复 | 到达后才计工作时间，取消/卡住/目标销毁释放资源并清理回调，完成只提交一次。 |

| SC04.5 | 跨房与真实计时 | 跨房遵守门/连接规则；游戏加速不放大真实步速或重复计算物理秒。 |



#### SC05 角色验收与测试系统


- 边界：独立于生产控制器，用正式角色和正式宿主采集证据并作断言；不建立测试专用世界、隐藏传送、状态注入或合成完成接口。

- 输入：正式新局、可观察角色/房间/物件、正常可执行操作和内容声明阈值

- 输出：逐用例测量、PASS/FAIL/未覆盖、原始观察/回执/实机截图和证据路径

- 依赖：SC01、SC02、SC04、SC06、SC07、A04

- 当前映射：已有真实宿主 MCP、角色回执与移动回归素材。旧 MovementRegression 包装另实例化 Main 并访问宿主内部成员，不直接视为符合当前正式入口/公开端口约定的验收实现；用例应迁入正式 MCP 证据流程。

- 代码：`mcp/game/automation.py`；`mcp/game/input_client.py`；`tools/diagnostics/character_receipts.py`；`tools/verification/MovementRegression.gd`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| SC05.1 | 用例选择与覆盖记录 | 记录角色、内容版本、seed、真实 backend、期限与自然到达范围；未达到条件记未覆盖。 |

| SC05.2 | 体型、净空与通行验收 | 实际验证不同正式角色的胶囊/身高、门洞、楼梯与窄处；阈值来自角色/几何声明，不临时编造通过值。 |

| SC05.3 | 位移、速度与动画验收 | 以真实物理位移验证速度、停步和脚底滑移；正常/加速/暂停分别采样，不以动画播放代替位移。 |

| SC05.4 | 座位、触及与交互验收 | 同一 execution 的到达、坐姿、接触误差、工作和完成证据可关联，碰撞和姿态分别判定。 |

| SC05.5 | 取消、打断与多人验收 | 测试公开取消、门阻断、槽位争用和跨房；观察超时/恢复/资源释放且不重复结算。 |

| SC05.6 | 报告与证据封存 | 每项给出断言、测量、阈值来源、事件 ID 和截图/回执路径；超时不是通过，未听音不报声学通过。 |



#### SC06 角色外观、动画与姿态系统


- 边界：负责角色资源装配、动画和视觉姿态；与 SC04 物理控制、SC05 验收分开。不是再造 Godot 动画引擎。

- 输入：角色资源、物理速度/动作阶段、对话动作、座位与触及目标

- 输出：可见角色、动画切换/结束、骨骼姿态与视觉诊断

- 依赖：A03

- 当前映射：已有导入角色、动画库、对话和座姿驱动。采用 AnimationPlayer/自定义库或 AnimationTree 是实现选择，不能把混合树列为本项目已用能力。

- 代码：`scenes/components/characters/ImportedCharacter.gd`；`scenes/components/characters/StoryBody3D.gd`；`scenes/components/characters/KayKitAnimationLibrary.gd`；`scenes/components/characters/ConversationAnimationLibrary.gd`；`scenes/components/characters/SeatPostureDriver3D.gd`；`scenes/components/characters/SeatPoseModifier3D.gd`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| SC06.1 | 模型、骨架与资源装配 | 尺度、骨架、碰撞参考与角色标识一致，资源缺失有明确反馈。 |

| SC06.2 | 移动动画与过渡 | 由实际位移/速度驱动走停和转向，撞墙不持续原地走；若启用 root motion 由 SC04 显式消费。 |

| SC06.3 | 行为与对话演出 | 动作 token/generation 与实际执行一致，旧完成回调不结束新动作。 |

| SC06.4 | 坐姿、触及与骨骼修正 | 按正式锚点对齐，修正不偷偷移动 Core/碰撞主身体；不同体型分别验证。 |



#### SC07 房间运行、门窗与空间连接系统


- 边界：负责已发布房间的运行生命周期、门窗状态/碰撞和跨房连接；不是编辑器修建，也不代替 Core 逻辑空间规则。

- 输入：正式房间/DV 元数据、逻辑门状态、人物/相机位置与驻留请求

- 输出：房间实例生命周期、门窗物理反馈、可用导航数据与空间端口

- 依赖：A02、A03、SC01、SC02

- 当前映射：已有房间流送、门窗组件和逻辑门策略。CampaignRoomHost 是多系统协调点，应按端口拆职责，不能直接当作某一个系统全部代码。

- 代码：`scenes/components/architecture/RoomStreamingController.cs`；`scenes/components/architecture/RoomContainer3D.gd`；`scenes/components/architecture/HingedDoor3D.gd`；`scenes/components/architecture/HingedWindow3D.gd`；`scenes/components/characters/CampaignRoomHost.gd`；`core/spatial/PortalPolicy.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| SC07.1 | 加载、驻留与卸载 | 房间进出不丢失 Core 运行态/证据，卸载释放呈现资源，未加载不误判角色不存在。 |

| SC07.2 | 门窗状态与碰撞同步 | 开闭/运动状态和逻辑许可一致；没有动态门叶不自动等于门已打开。 |

| SC07.3 | 导航与跨房连接更新 | 房间或门变化后路径可用性及时更新，导航同步尚未完成时不会假报到达。 |

| SC07.4 | 空间注册和交互端口 | 人物/物件/槽位按稳定 ID 注册；失效节点及时注销，拒绝错误世界/代际绑定。 |



#### SC08 镜头、书房舞台与环境呈现系统


- 边界：管理观察镜头、书房/3D 书叶、环境光照和项目材质/可见性策略；声音由 UI03，世界时间由 Core。

- 输入：模式、相机目标、正式时间/环境、书页纹理和减少动态设置

- 输出：当前相机、实机画面、书叶演出、坐标映射与渲染诊断

- 依赖：A03

- 当前映射：已有舞台、翻页、相机、遮挡和环境呈现组件；标题与书 UI 共用舞台。镜头/材质变动不能修改物理规则或人物知识。

- 代码：`scenes/components/world/StudyDeskStage3D.gd`；`scenes/components/world/BookLeafTurn3D.gd`；`scenes/components/rendering/TopDownCamera3D.gd`；`scenes/components/rendering/CameraOcclusion.gd`；`scenes/components/rendering/WorldTimeLighting.gd`；`scenes/components/rendering/ObserverEnvironment3D.gd`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| SC08.1 | 镜头控制和模式交接 | 标题、书本、现场相机切换正确，操作与 HUD 投影一致，不遗留失效 current camera。 |

| SC08.2 | 书房、3D 书叶与纹理映射 | 正反页、UV、翻页方向与 UI05 一致，减少动态设置改变演出但不改变内容。 |

| SC08.3 | 遮挡、剖切与可读性 | 人物/目标可辨识，透明/剖切只改变呈现；物理碰撞与逻辑视听规则分别保留。 |

| SC08.4 | 环境、材质和性能策略 | 照明按正式时间更新，资源/实例预算可测；不为了帧率悄悄减少逻辑模拟和证据。 |



### UI / 场景共同契约


- **A01 模式、输入与暂停协调**：Main/宿主统一协调 title/field/book/推演/修建等模式、输入焦点和暂停原因。设计上各模式申请/释放暂停原因，最后一个原因解除才恢复；物理、Core tick、执行队列、动画和音频分别规定策略。 这是共享职责设计，不宣称已有 pause-token 公共 API。

- **A02 会话观察与操作网关**：UI/场景消费授权只读投影并提交动作意图/物理完成证据；Core 负责真值、认知、证明、计时和存取。执行标识、角色/物件/槽位 ID 和代际必须一致。 复用 Main/SessionBridge 的现有公开接口，具体改接口时更新 docs/31 第五节。

- **A03 内容与资源契约**：统一坐标/尺度、稳定 ID、预制件/结构版本、锚点/碰撞层、候选/发布和资源引用；初始人物/规则/布置通过 DV 声明，C# 与 GDScript 不重复硬编码。 共享数据契约不等于建设一个包办所有功能的全局 Manager。

- **A04 真实宿主证据流程**：自动验收用 CliMCP/GodotMCP，AI/人工按 on/step/observe/close；每轮正式新局，保存原始返回、截图和事件。PASS/FAIL/未覆盖分开；预览、超时和静态扫描不冒充实机通过。 SC05 规定角色用例，A04 提供真实宿主操作和证据保管；不向生产角色加入绕过规则的测试 API。


### Godot 内置能力及项目职责


| 内置能力 | Godot 原语 | 项目仍负责 | 系统归属 |

| --- | --- | --- | --- |

| 控件、布局、主题与输入焦点 | Control / Container / CanvasLayer / Theme | 界面状态、布局参数、可读性、模态优先级和防穿透 | UI01、UI02、UI04、UI05、A01 |

| 子视口与渲染纹理 | SubViewport / ViewportTexture | 书页 UV/屏幕坐标、输入转发、翻页同步 | UI05、SC08 |

| 暂停与节点处理模式 | SceneTree.paused / Node.process_mode | 跨 Core、物理、队列、UI、动画、音频的暂停范围与恢复条件 | A01 |

| 音频播放与混音 | AudioServer / Audio buses / AudioStreamPlayer(3D) | 声音事件、选曲、并发、空间监听/遮挡策略、清理与暂停 | UI03 |

| 网格构建 | ArrayMesh / SurfaceTool | 结构拓扑、洞口、尺寸约束、碰撞及导航源一致性 | SC01 |

| 场景和资源序列化 | PackedScene / ResourceLoader / ResourceSaver | owner、稳定 ID、资源依赖、候选/发布和 DV 一致性 | SC02、SC03、SC07 |

| 编辑器扩展 | EditorPlugin / @tool / gizmo | 工具模式、编辑命令、参数校验；编辑器代码不进入运行时依赖 | SC03 |

| 撤销重做基础 | EditorUndoRedoManager / UndoRedo | 命令事务、引用寿命、跨操作一致性；运行时不使用 EditorUndoRedoManager | SC03 |

| 角色物理及碰撞查询 | CharacterBody3D / CollisionShape3D / physics queries | 速度、层/掩码、坡阶参数、阻断恢复与可测量的动作阶段 | SC04、SC05 |

| 导航计算 | NavigationServer3D / NavigationAgent3D / NavigationRegion3D | 导航数据/同步、跟随与物理移动、门连接、资源槽；有路径不等于实际到达 | SC04、SC07 |

| 动画、骨架和混合 | AnimationPlayer / AnimationTree / Skeleton3D | 角色动作映射、token、姿态、触及和 root-motion 运动主权 | SC06 |

| 基础渲染与场景树 | Camera3D / Environment / lights / scene instances | 镜头策略、剖切、材质风格、房间驻留及项目性能预算 | SC08、SC07 |



### UI / 场景整合闭环


- **I01 暂停书本闭环**：现场动作 → 打开 UI05 → A01 暂停 → SC08 书页/镜头 → UI03 菜单音 → 关闭恢复。断言：书可操作；角色/Core 不偷跑；翻页点击不下落到现场；解除本次暂停不误解除其他暂停原因。

- **I02 推演闭环**：UI02 提示 → UI01 选择证据 → A02/Core 校验 → UI01/UI02 反馈。断言：使用本人当前证据；失败原因可见；关闭浮层不提交草稿；公开 UI 不泄漏 NPC 私有状态。

- **I03 房间制作闭环**：SC03 编辑 → SC01/SC02 生成 → 候选保存重载 → 明确发布/DV 校验 → SC07 正式加载。断言：候选不污染正式资源；重载保留实例/锚点；发布后视觉、碰撞、导航和 DV 元数据一致。

- **I04 角色交互闭环**：UI02 动作 → A02 → SC04 移动/对齐 → SC06 姿态 → SC02/SC07 物件 → Core 完成 → SC05 记录。断言：真实到达和接触、工作计时、取消/释放及最多一次完成都有同 execution 的证据。


## 9. Core：目标功能设计


先完成独立生活循环，再按显式依赖装配特色能力；基础模块不调用特色实现。即使关闭扩展，客观事实和人物可用信息仍分开保存。


### 生活基础


#### B01 实体、客观状态与事务


- 输入：DV 实体/初值、合法效果事务

- 输出：客观事实快照、修订号、变化事件

- 依赖：无业务模块依赖

- 当前映射：已有 FactStore、条件/效果和值语义；ontology 目录也含人物心智相关原语，不能按目录整个视为可关闭认知。

- 代码：`core/ontology/State.cs`；`core/ontology/Primitives.cs`；`core/ontology/Events.cs`；`core/ontology/Snapshots.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| B01.1 | 实体与字段生命周期 | ID、类型、初值和引用合法；人物可用信息不作为客观真值。 |

| B01.2 | 效果事务与变化集 | 开始动作不提前结算；同 execution 的成功效果最多提交一次。 |

| B01.3 | 快照与修订 | 快照不泄漏可变对象，变化通知可关联其事务。 |



#### B02 时间、日历与世界事件


- 输入：宿主时间、DV 定时/条件规则

- 输出：世界时间、到期事件、变化队列

- 依赖：B01

- 当前映射：已有共享时钟、逻辑/物理待推进量、索引式世界规则与计时检查点；通用周期日历需另核对补齐。

- 代码：`core/simulation/SimulationClock.cs`；`core/simulation/WorldRules.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| B02.1 | 推进、速度与暂停 | 暂停不偷跑，物理秒与游戏秒区分，预算不足保留待处理工作。 |

| B02.2 | 条件与定时事件 | 按依赖变化触发；过期计时代际不得重复执行。 |

| B02.3 | 日程时间服务 | 周期任务和事件按声明时刻触发，不依赖某一帧恰好命中。 |



#### B03 需求、性格、情绪与状态效果


- 输入：DV 需求、衰减、偏好与实际完成事件

- 输出：需求值、情绪/Buff、生活效用贡献

- 依赖：B01、B02

- 当前映射：已有解析衰减、需求评分、Buff/情绪与重配置保留需求；性格主要由声明权重体现，不假定完整人格系统已完成。

- 代码：`core/autonomy/DriveCalculator.cs`；`core/autonomy/AutonomyDriveSolver.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| B03.1 | 需求衰减与满足 | 预测收益不改变真实需求，实际完成/失败按声明更新。 |

| B03.2 | 偏好与性格权重 | 不同角色对同一可用物件的生活效用可以不同，参数来自 DV。 |

| B03.3 | 情绪与 Buff 生命周期 | 获得、持续、过期和主情绪变更可解释且可存取。 |



#### B04 物件交互与基础感知目录


- 输入：物件动作、角色权限、可达/可见信息

- 输出：生活动作候选、客观执行条件、预期收益

- 依赖：B01、B07

- 当前映射：已有 ActionCatalog 与 Perception；当前候选 Recognition 读取 ActorClient.Recall。需要抽出基础观察适配，才能脱离高级证据处理。

- 代码：`core/simulation/RuntimeContent.cs`；`core/cognition/Perception.cs`；`core/autonomy/DecisionScheduler.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| B04.1 | 物件提供的交互 | 床、食物、座位等按声明提供动作、时长、槽位和预期生活收益。 |

| B04.2 | 基础感知与熟悉位置 | 只提供当前可感知、自身状态和正常获知/记住的位置；远处隐藏物品不自动全知。 |

| B04.3 | 候选与执行准入分离 | 人物认为可做不等于客观可执行；门锁或物件失效时返回正常失败。 |



#### B05 生活自主决策与玩家队列


- 输入：基础观察、需求、生活目标、合法候选

- 输出：下一动作、排队/取消意图与选择原因

- 依赖：B03、B04

- 当前映射：已有候选索引、效用评分、TaskPlanner/GoalPlanningAdapter 和失败记忆；它们与 ActorClient、规则和规划混合。Sim4Planner 是项目算法名称，不能当作模拟人生4真实实现的证明。

- 代码：`core/autonomy/DecisionScheduler.cs`；`core/autonomy/GoalPlanning.cs`；`core/autonomy/Sim4Planner.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| B05.1 | 单步生活效用选择 | 无特色模块时仍能按需求收益、时间/距离、偏好、失败代价选择吃喝睡和社交。 |

| B05.2 | 玩家命令与自主优先级 | 手动队列、暂停自主、取消和恢复有一致规则；关闭自主不关闭生活仿真。 |

| B05.3 | 生活任务与重选 | 取食再进食等普通步骤可重校验；无需案件证明或反事实做局才能完成。 |



#### B06 行动执行、资源预留与结算


- 输入：决策/玩家动作、正常宿主完成证据

- 输出：执行阶段、占用状态、成功/失败回执与事务

- 依赖：B01、B02、B04

- 当前映射：RuntimeSession 已有到达后工作、宿主完成、取消、槽位与 execution；当前同类还持有推演、策略、对话等协调职责，需逐个收窄。

- 代码：`core/simulation/RuntimeSession.cs`；`core/spatial/SlotReservations.cs`；`core/simulation/ConversationRuntime.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| B06.1 | 动作状态机 | 排队、行走、对齐、工作、完成、失败/取消分明，未到达不开始现场工作。 |

| B06.2 | 资源预留与争用 | 多人争座/取食按容量裁决，取消或失效释放占用。 |

| B06.3 | 真实完成与幂等结算 | 同 execution/代际关联宿主证据；迟到回调不修改新任务。 |



#### B07 逻辑空间、房间与通行


- 输入：发布房间图、门许可、逻辑位置与宿主空间观察

- 输出：路径/可达性、房间驻留和交互位置约束

- 依赖：B01

- 当前映射：已有房间图、门策略、房间生命周期和座位适配；Godot 碰撞/移动仍由场景 SC04/SC07 执行。

- 代码：`core/spatial/NavigationGraph.cs`；`core/spatial/PortalPolicy.cs`；`core/spatial/RoomLifecycle.cs`；`core/spatial/SeatingFit.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| B07.1 | 拓扑与门许可 | 锁门、断路和门状态变化能使路径失效并正常重选。 |

| B07.2 | 逻辑位置与房间生命周期 | 加载/卸载不改变客观物件事实，房间迁移与公开位置一致。 |

| B07.3 | 交互几何契约 | 站位、座位和体型约束有声明来源；逻辑可达不能冒充实机无碰撞。 |



#### B08 日常社交与关系


- 输入：普通社交意图、关系/偏好、交谈完成事件

- 输出：交流状态、社交满足、关系变化

- 依赖：B01、B03、B06

- 当前映射：已有对话会话、对齐、逐句推进、中断和叙述回执；完整通用关系网络未核实，不把案件说服直接当日常社交全实现。

- 代码：`core/simulation/ConversationRuntime.cs`；`core/simulation/RuntimeSession.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| B08.1 | 交谈时序与参与者 | 双方正常到达、占用、轮次和中断清理正确。 |

| B08.2 | 生活社交结果 | 可按声明影响社交需求与关系，不要求生成逻辑命题或证明。 |

| B08.3 | 关系与熟悉度 | 关系变化归属于真实互动，私有关系投影只给获准观察者。 |



#### B09 物资、持有、库存与经济


- 输入：DV 物品、库存/持有、取得消耗和交易声明

- 输出：物资转移、所有权/数量和账目变化

- 依赖：B01、B06

- 当前映射：内容已有取食、携带、交接、消耗和库存条件，通用钱财/购买/账单系统没有据此成立。此系统是类型补全，首期可只实现物资子集。

- 代码：`core/simulation/CharacterSessionDef.cs`；`core/simulation/RuntimeSession.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| B09.1 | 库存与消耗 | 最后一份物资不能被多人重复取得，完成与取消不重复增减数量。 |

| B09.2 | 持有、转交与所有权 | 携带和交接由真实动作完成，物件来源、去向可追踪。 |

| B09.3 | 货币与家庭账目 | 如启用购买/账单，收支和退款作为事务；未声明经济内容时不强制建完整货币循环。 |



#### B10 技能、日程、职业与生活目标


- 输入：DV 成长/任务定义、实际行动与日程事件

- 输出：经验、解锁、任务进展和生活目标

- 依赖：B01、B02、B06

- 当前映射：已有目标规划和内容条件基础；通用技能成长、职业晋升与周期日程不是 GoalPlanning 命名即证明完成，需按生活内容补齐。

- 代码：`core/autonomy/GoalPlanning.cs`；`core/simulation/CharacterSessionDef.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| B10.1 | 技能与成长解锁 | 只由声明的真实行动获得经验，升级不重复奖励。 |

| B10.2 | 周期日程与工作 | 到点、缺席、中断、休息和回归可处理，不依赖案件章节。 |

| B10.3 | 生活目标与任务进展 | 目标用生活事实判断，关闭推理仍可完成普通生活任务。 |



#### B11 住户、家庭与生命历程


- 输入：DV 住户、家庭归属、生命阶段与健康声明

- 输出：成员/控制关系、生活阶段、健康及生命周期事件

- 依赖：B01、B02、B03

- 当前映射：已有角色装配、初值和失能关联；家庭管理、生育、年龄增长与完整健康系统未核实。保留类型边界，不要求首期复制模拟人生全品类。

- 代码：`core/simulation/CampaignScenario.cs`；`core/simulation/CharacterSessionDef.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| B11.1 | 住户与可控成员 | 玩家控制归属、家庭成员和住户迁移使用稳定 ID。 |

| B11.2 | 健康与基础失能 | 普通疾病/疲劳等按生活规则处理；关闭源质侵蚀不关闭普通健康限制。 |

| B11.3 | 年龄与生命阶段 | 如内容启用成长/死亡，变化只发生一次，引用和占用清理有明确规则。 |



### 特色扩展


#### X01 证据溯源与独立人物信念


- 输入：真实感知、合法消息和原始证据

- 输出：有来源、时效、冲突与置信信息的人物认知

- 依赖：B01、B04

- 拟定能力键：`epistemic_evidence`（待实现设计）

- **关闭后：使用 B04 基础观察/有限生活记忆，不运行高级证据聚合；仍然不读取世界全量隐藏状态。**

- 当前映射：已有证据/ActorClient 状态与修订；基础观察和高级认知仍需拆开适配。

- 代码：`core/cognition/BinaryEvidence.cs`；`core/cognition/Awareness.cs`；`core/autonomy/GoalPlanning.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| X01.1 | 来源与传播链 | 每个高级知识更新可追溯感知/消息/推演来源。 |

| X01.2 | 冲突、置信与时效 | 未知、否定和冲突不混淆；重复证据不重复计权。 |

| X01.3 | 人物隔离与重载 | 不同人物互不读私有信念；重载保留证据和运行认知。 |



#### X02 命题通信、信任与欺瞒


- 输入：发送者所持消息、通道、接收者认知

- 输出：实际送达消息、可信度影响与通信证据

- 依赖：X01、B08

- 拟定能力键：`epistemic_communication`（待实现设计）

- **关闭后：B08 普通交谈与关系效果保留；不创建案件命题、证言或谎言判定。**

- 当前映射：已有通信、接收者和叙述/证据关联；高级命题结果与生活社交时序需分别装配。

- 代码：`core/cognition/ConversationMind.cs`；`core/simulation/ConversationRuntime.cs`；`core/simulation/RuntimeSession.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| X02.1 | 可达、送达与来源 | 只有真实收听/送达者获得消息，UI 文字出现不自动等于听见。 |

| X02.2 | 信任与佐证影响 | 可信度按声明及接收者所知处理，不读取全知真相替角色裁决。 |

| X02.3 | 隐瞒与错误信息 | 说话内容、说话者信念和客观真假分别记录。 |



#### X03 人物解释、规范采纳与说服


- 输入：本人证据、请求、价值偏好和采纳规则

- 输出：个人解释、采纳/拒绝和可公开原因

- 依赖：X01、B05、B08

- 拟定能力键：`personal_interpretation`（待实现设计）

- **关闭后：保留生活偏好和普通社交接受/拒绝；去掉证据/规范博弈门槛，不把普通开门吃饭锁死。**

- 当前映射：已有个人可采纳/解释函数和说服相关机制；其中兼容函数是否走当前主链需按入口核对。人物规则采纳不等于注册客观世界规则。

- 代码：`core/cognition/PersonalPolicy.cs`；`core/simulation/WorldRules.cs`；`core/simulation/RuntimeSession.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| X03.1 | 个人规则与价值解释 | 角色只基于自己的规则/所知评估，不覆盖客观物理许可。 |

| X03.2 | 请求与证据说服 | 有无已送达佐证影响采纳；重复提交不重复结算。 |

| X03.3 | 采纳结果与隐私 | 公开原因与私有权重分离，拒绝不泄漏隐藏心智。 |



#### X04 形式推演与证明校验


- 输入：本人证据、规则、推演目标与提交

- 输出：结论、证明证书或失败原因

- 依赖：X01

- 拟定能力键：`formal_reasoning`（待实现设计）

- **关闭后：无推演候选、自动证明或推演 UI 提交；吃饭睡觉、日常聊天与普通生活目标不需要证明。**

- 当前映射：已有自然演绎、案件理论、独立证书校验和 RuntimeSession deduction 绑定；基础条件求值仍保留，不与高级推理一并关闭。

- 代码：`core/reasoning/NaturalDeduction.cs`；`core/reasoning/CaseTheory.cs`；`core/reasoning/CaseProofChecker.cs`；`core/simulation/RuntimeSession.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| X04.1 | 命题与规则编译 | 规则/目标来自 DV；证明的输入是本人证据投影。 |

| X04.2 | 推演搜索与预算 | 时间/步数耗尽给出未完成，不冒充无解或成功。 |

| X04.3 | 独立证书验证 | 陈旧、伪造或缺前提证明被拒，失败不修改事实。 |



#### X05 反事实模拟、策略与做局


- 输入：本人模型、可采取动作、目标和假设分支

- 输出：预测分支、策略选择和待执行计划

- 依赖：X01、X04、B05、B06

- 拟定能力键：`counterfactual_strategy`（待实现设计）

- **关闭后：B05 单步生活效用及普通任务步骤继续；不生成反事实案件策略或做局动作。**

- 当前映射：已有策略活动、分支与策略绑定；普通手段目的规划和特色反事实规划应拆为不同评分/候选贡献，不能整体删除 GoalPlanning 文件。

- 代码：`core/reasoning/CaseStrategy.cs`；`core/reasoning/CaseBranches.cs`；`core/autonomy/GoalPlanning.cs`；`core/simulation/RuntimeSession.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| X05.1 | 假设分支 | 模拟只操作分支副本，不把预测写成已发生事实。 |

| X05.2 | 策略选择与证据条件 | 策略只能使用本人模型，预测收益和客观可执行性分开。 |

| X05.3 | 执行与重新校验 | 每一步执行前按新状态复核，预测正确不等于动作成功。 |



#### X06 源质侵蚀与认知退化


- 输入：DV 侵蚀来源、阈值、时间与人物状态

- 输出：侵蚀阶段、能力限制与记忆变化

- 依赖：X01、B02、B03、B11

- 拟定能力键：`erosion`（待实现设计）

- **关闭后：从新局不装配侵蚀累积、侵蚀失能和侵蚀遗忘；普通疲劳/疾病仍由 B03/B11 处理。**

- 当前映射：兼容核心可见 erosion 阈值、记忆截断与调度调用；正式新局的注册/推进路径仍须逐条证明，不能只凭旧函数认定有效。

- 代码：`core/cognition/ActorCognition.cs`；`core/autonomy/DecisionScheduler.cs`；`core/simulation/RuntimeSession.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| X06.1 | 侵蚀量与阶段 | 变化来自声明和真实事件，阈值不在宿主重复硬编码。 |

| X06.2 | 能力与行动限制 | 每个限制能追溯侵蚀阶段，基础生命/健康规则独立。 |

| X06.3 | 遗忘与证据保管 | 按声明改变人物可用记忆；审计证据不伪造、不静默抹除。 |



#### X07 案件、世界特色规则与叙事进展


- 输入：DV 案件/世界特色规则、合法事件和已证结论

- 输出：章节目标、特色事件和结局判定

- 依赖：X01、X02、X04、B02、B06

- 拟定能力键：`case_narrative`（待实现设计）

- **关闭后：加载生活内容包；不注册案件/源质专属触发器，也不让证明或剧情完结成为生活运行前提。**

- 当前映射：现有 CampaignScenario/CharacterSessionDefinition 以正式内容装配案件与世界机制；尚无证据证明已支持生活模式的能力解析与完整内容裁剪。

- 代码：`core/simulation/CampaignScenario.cs`；`core/simulation/CharacterSessionDef.cs`；`core/reasoning/CaseHistory.cs`；`core/simulation/WorldRules.cs`


| 二级编号 | 职责 | 验收条件 |

| --- | --- | --- |

| X07.1 | 内容能力要求 | 动作、目标、规则和初值声明所需能力；禁用内容被明确裁剪或报错。 |

| X07.2 | 特色世界机制与叙事触发 | 生活定时/物理规则仍保留；特色规则仅在启用并满足依赖时注册。 |

| X07.3 | 章节与结局 | 已发生事实/合法结论推动剧情；生活模式不因缺案件结局卡死。 |



### Core 共同基础设施


- **K01 DV 内容与能力装配**：C# DV 解析器读取声明的能力清单、内容 requires 和版本；新局前解析依赖并决定注册哪些服务/规则/动作。这里的清单与 requires 是待实现设计，不宣称已有 DV 语法。

- **K02 会话门面与宿主端口**：统一授权观察、提交、推进和真实完成接口；CLI/Godot 共用 Core。生活/高级认知可复用同一类型结构或用适配器，生活路径不得强制等待高级推理完成。

- **K03 存取、版本与内容重载**：存档保存有效能力集合、解析器/模块 schema、内容哈希、seed/随机状态、世界/认知/执行检查点；首次兼容旧存档须显式识别旧全功能模式。内容热重载不重置事实或证据。

- **K04 预算、审计与真实宿主验收**：按基础/扩展分列耗时、任务和回执；禁用扩展调用数应为零。CLI/Godot 使用正式新局和 MCP 验收，静态扫描、构建或界面隐藏不作为功能关闭证明。


### 模式预设（设计，非运行配置）


| 预设 | 开启特色 | 行为 |

| --- | --- | --- |

| life 生活模拟 | 全部关闭 | 生活需求→物件候选→效用决策/玩家命令→真实行动→结算→日常社交；基础观察保留。 |

| cognitive 认知生活 | X01、X02、X03 | 生活循环加个人证据、消息与说服；不运行证明、反事实做局或侵蚀。 |

| reasoning 推演与策略 | X01、X02、X03、X04、X05 | 增加形式推演和做局；侵蚀与案件章节仍可不启用。 |

| full 本游戏完整模式 | X01、X02、X03、X04、X05、X06、X07 | 启用全部特色；仍使用同一套生活基础、行动状态机和客观状态。 |



### 关闭与存档政策


1. 能力配置首期在新局生效并固定到存档；不支持任意存档中途切模式。内容重载不得偷偷改变能力集。

2. 依赖组合不合法时拒绝启动并列出缺失项，不静默自动开启用户关闭的能力。关闭上游的配置编辑可提示显式一并关闭哪些下游。

3. 禁用必须覆盖注册/定时、动作候选/自动决策贡献、结算钩子、观察和 UI；隐藏按钮或返回常量不算关闭。

4. 移除禁用动作后重新校验生活必需资源和可达动作：吃喝睡、交谈等不能只剩需要证明或说服的路线。

5. 基础模式的日常关系判定、条件求值、简单任务步骤和基础记忆仍可存在；高级证据/案件规则才由扩展开关控制。

6. 将来如需中途切换，必须单独设计执行取消/占用释放、规则计时器卸载、缓存失效和迁移；侵蚀造成的旧事实不能靠关闭开关自动治愈。


基础观察不等于全知；X03 可基于直接观察证据工作，消息说服才需要 X02。X07 中用到做局/侵蚀的具体案件内容须额外声明 X05/X06。生活品类 B09–B11 可以先提供当前内容需要的子集，不因列出职责而宣称经济、家庭与职业已完成。


### Core 七条整合验收契约（尚未执行）


| 用例 | 条件 | 断言 |

| --- | --- | --- |

| C01 无特色也能生活 | life 正式新局，使用真实生活内容 | 实际到达吃喝睡、交谈和资源争用；完成/取消正确，X01–X07 无注册与运行贡献。若生活内容或观察端口尚缺则记未覆盖。 |

| C02 扩展依赖与内容拒绝 | 正式启动配置 X05=true、X04=false，及有禁用能力引用的内容包 | 启动前明确拒绝并指出依赖或引用，不能悄悄启用推理或运行半套案件。该配置入口尚待实现。 |

| C03 关闭策略仍有自主生活 | 认知生活或推演模式新局，不启用 X05 | 生活效用选择和普通步骤任务继续，没有反事实策略候选/评分/提交。 |

| C04 关闭侵蚀不关闭健康 | 依赖合法的新局，X06=false，正式普通疲劳/健康条件 | 无侵蚀累积、失忆/侵蚀失能；普通疲劳、休息与健康约束继续有效。 |

| C05 特色逐层增加行为 | 相应 profile 的独立正式新局 | 认知、证明、策略各有真实原始证据；开启后增加的候选/原因有模块归属，基础执行阶段不另起一套。 |

| C06 同模式存取与能力不匹配 | 正式保存并继续，同一内容/能力版本；另用不匹配配置打开 | 合法继续保留状态/证据/执行；不匹配拒绝或走显式迁移，不清空心智再装成成功。 |

| C07 基础边界不反向依赖 | 真实入口依赖核对、基础路径构建及宿主调用计数 | 基础不引用特色实现；编译时可引用抽象契约，但 life 不要求初始化特色全局 Hook。编译通过与玩法通过分开记录。 |



## 10. 当前物理工程依赖


以下从当前 `.csproj` 的 ProjectReference 读取。箭头方向为调用方 → 依赖方；不是简化层级图。


| 工程 | 框架 | 引用工程 | 显式编译项 |

| --- | --- | --- | --- |

| DualOntologyWorldReconstruct.csproj | net8.0 | core/session_cli/DuelView.Cli.csproj | {'Include': 'scenes/**/*.cs'} |

| core/autonomy/DuelView.Autonomy.csproj | net8.0 | core/dv_lang/DuelView.DvLang.csproj；core/ontology/DuelView.Ontology.csproj；core/cognition/DuelView.Cognition.csproj；core/reasoning/DuelView.Reasoning.csproj；core/spatial/DuelView.Spatial.csproj | 默认编译项 |

| core/cognition/DuelView.Cognition.csproj | net8.0 | core/dv_lang/DuelView.DvLang.csproj；core/ontology/DuelView.Ontology.csproj | 默认编译项 |

| core/dv_lang/DuelView.DvLang.csproj | net8.0 | 无 | 默认编译项 |

| core/ontology/DuelView.Ontology.csproj | net8.0 | core/dv_lang/DuelView.DvLang.csproj | 默认编译项 |

| core/reasoning/DuelView.Reasoning.csproj | net8.0 | core/dv_lang/DuelView.DvLang.csproj；core/ontology/DuelView.Ontology.csproj | 默认编译项 |

| core/session_cli/DuelView.Cli.csproj | net8.0 | core/dv_lang/DuelView.DvLang.csproj；core/ontology/DuelView.Ontology.csproj；core/cognition/DuelView.Cognition.csproj；core/reasoning/DuelView.Reasoning.csproj；core/spatial/DuelView.Spatial.csproj；core/autonomy/DuelView.Autonomy.csproj；core/simulation/DuelView.Simulation.csproj | {'Include': '../../tools/story/ScenarioAuthoring.cs', 'Link': 'ScenarioAuthoring.cs'} |

| core/simulation/DuelView.Simulation.csproj | net8.0 | core/dv_lang/DuelView.DvLang.csproj；core/ontology/DuelView.Ontology.csproj；core/cognition/DuelView.Cognition.csproj；core/reasoning/DuelView.Reasoning.csproj；core/spatial/DuelView.Spatial.csproj；core/autonomy/DuelView.Autonomy.csproj | 默认编译项 |

| core/spatial/DuelView.Spatial.csproj | net8.0 | core/dv_lang/DuelView.DvLang.csproj；core/ontology/DuelView.Ontology.csproj | 默认编译项 |



特别关注：Godot 根工程引用 DuelView.Cli；CLI 显式链接 tools/story/ScenarioAuthoring.cs。功能模块设计没有改变这些实际引用。


## 11. 旧 16 个技术归属单元（检索参考）


| 编号 | 名称 | 代码路径模式 | 边界 |

| --- | --- | --- | --- |

| M01 | DV 内容读取 | core/dv_lang/*；assets/data/*；config/* | 只负责声明读取、bundle 合并、schema 与内容哈希；不推进运行状态。案件专用声明归 M04，其余 DV 由本模块拥有。 |

| M02 | 本体与值存储 | core/ontology/* | 字段引用、事实、值、快照、变更与旧 DVKernel 基础原语；不直接接受玩家输入。 |

| M03 | 感知与人物认知 | core/cognition/* | 角色局部投影、证据、置信度、侵蚀与个体记忆；不能从规划器回写客观真值。 |

| M04 | 案件推理与证明 | core/reasoning/*；assets/data/cases/* | 理论、规则、带符号结论、证明搜索与独立校验；只输出证书和候选，不直接执行世界动作。 |

| M05 | 逻辑空间与资源预留 | core/spatial/* | 导航拓扑、门策略、可视性、房间驻留与槽位；不负责 Godot 碰撞/动画。 |

| M06 | 需求与自主决策 | core/autonomy/* | 需求效用、任务规划、失败记忆与调度选项；不直接提交世界效果。 |

| M07 | 运行会话与世界仿真 | core/simulation/* | 唯一协调 World、人物、规则、时钟、动作、对话及战役；接受宿主完成证据并负责结算。 |

| M08 | CLI 适配与兼容会话 | core/session_cli/* | NativeCli 输入/输出、审计、合法选项与兼容 DvSession；不新增另一套世界规则。 |

| M09 | Godot 会话网关 | scenes/main/*；project.godot；DualOntologyWorldReconstruct.csproj；DuelView.sln | Main 生命周期/界面编排、SessionBridge 值转换与审计、存取和内容重载；同场只拥有一个正式 Core 会话。 |

| M10 | 角色行动与物理执行 | scenes/components/characters/* | CampaignRoomHost、动作队列、马达、对话、坐姿和房间切换；只回报物理结果，不重复 C# 规则。 |

| M11 | 建筑与房间流送 | scenes/components/architecture/*；scenes/prefabs/*；scenes/rooms/* | 原生墙/楼梯/门窗/道具、房间实例、剖切和流送；消费逻辑门/房间事实，物理结果交给角色执行层。 |

| M12 | 玩家界面 | scenes/UIpanel/*；assets/localization/*；assets/ui/* | 标题、GUI、书本/存档/设置及文本资源；只调用 Main/现场公开接口，不读 Core 或宿主私有状态。 |

| M13 | 舞台渲染与共享服务 | scenes/components/world/*；scenes/components/rendering/*；scenes/components/shared/*；scenes/World/*；assets/*；ShareAsset/* | 书房/书叶、环境照明、材质、音频、语言/设置服务与原始美术资产；不做剧情决策。 |

| M14 | 内容与资产制作发布 | tools/building/*；tools/godot_architect/*；tools/web_building_editor/*；tools/studio_web/*；tools/story/*；tools/tuning/*；tools/audio/*；mcp/asset_pipeline/*；mcp/build/* | 作者工具、编辑器、构建/资产流水线、调优与发布；输出候选，只有明确发布才进正式资源。 |

| M15 | 真实宿主测试基础设施 | mcp/game/* | 环境准备、构建缓存、启动/关闭、输入、截图与自动测试时限；不实现游戏规则或制造动作结果。 |

| M16 | 审计诊断与报告 | tools/ci/*；tools/diagnostics/*；tools/verification/*；tools/sandbox/*；tools/report/*；mcp/verification/*；mcp/spatial/*；MECHANISM_MAP.yaml | 静态隔离、诊断、回执审查与可视化报告；消费源码/原始证据，不生成玩法成功事实。 |



## 12. 目录职责与源码规模


下表规模来自 2026-11-10T08:47:42+08:00 的源码快照。命名函数是静态识别数量，不代表逐函数动态语义已经证明。


| 目录模块 | 职责 | 代码文件 | 命名函数 |

| --- | --- | --- | --- |

| assets/materials | Godot 材质着色器：书页、场景表面、海面、天空、灯光与效果。 | 38 | 98 |

| assets/shaders | Godot 着色器源文件；与 materials 中部分文件可能同内容，按哈希对照。 | 37 | 101 |

| core/autonomy | 需求效用计算、目标/任务规划、决策调度及 Sim4 规划；区分感知预期收益与完成后的实际效果。 | 5 | 246 |

| core/cognition | 主体感知与证据投影、置信聚合、认知侵蚀、个人策略和会话心智；以角色所知约束决策。 | 7 | 171 |

| core/dv_lang | 读取 .dv 文档及 bundle，形成 JSON 声明、内容哈希；提供兼容历史脚本读取与 C# 内容导出。 | 4 | 56 |

| core/ontology | 值运算、快照、字段引用与 FactStore；也保留 DVKernel 的列表/键值表示与事件、成本、论证机制。 | 9 | 734 |

| core/reasoning | 案件理论读取、带符号事实/规则、分步证明搜索、独立证明校验、历史与做局分支。 | 6 | 227 |

| core/session_cli | 当前 NativeCli 标准输入宿主及会话 API、观察、审计、存档/回放与验证；包含旧 DvSession 兼容实现。 | 9 | 702 |

| core/simulation | RuntimeSession 组合 World、人物、时钟、规则、调度、空间与机制；同时保留 object 形态的 DVSimulationKernel。 | 8 | 421 |

| core/spatial | 导航与房间图、门洞可视性、房间生命周期、交互槽预留、座位适配；不依赖 Godot 节点。 | 5 | 100 |

| mcp/asset_pipeline | 章节绘制、角色准备和渲染等离线资产处理。 | 15 | 245 |

| mcp/build | 构建会话与 DV 工具链的 Python 入口。 | 2 | 13 |

| mcp/game | MCP 服务、环境准备、构建缓存、进程运行、输入、自动化与视口截图。 | 13 | 56 |

| mcp/spatial | 空间专项审计工具。 | 1 | 8 |

| mcp/verification | 现存批次验证与 CLI 报告代码；存在不代表当前门禁有效。 | 5 | 35 |

| scenes/UIpanel | 标题、存档、设置、书页、现场 GUI；通过 Main/现场公开接口读取观察和提交操作。 | 11 | 147 |

| scenes/World | 地图/场域的外观与引用场景组件。 | 1 | 2 |

| scenes/components/architecture | 参数化墙/楼梯/地面、门窗与道具；C# 房间流送和剖切几何配合原生 Godot 场景。 | 15 | 160 |

| scenes/components/characters | 现场主控、动作队列、执行器、物理移动、对话动画、姿态与房间切换；回报宿主真实完成结果。 | 18 | 316 |

| scenes/components/rendering | 观察环境、世界时间照明与渲染着色器。 | 44 | 155 |

| scenes/components/shared | 语言、设置、声音、音乐上下文、材质控制及宿主生命周期。 | 7 | 87 |

| scenes/components/world | 书房舞台、书叶与桌面呈现；通过 SubViewport 与坐标映射显示 UI 纹理。 | 6 | 59 |

| scenes/main | Main 编排标题/书本/现场，SessionBridge 连接 RuntimeSession，另外处理展示声明、Godot 值转换和书本存档。 | 5 | 177 |

| tools/audio | 音效合成工具。 | 1 | 10 |

| tools/building | 房间组合、模型导出、资产打包和发布工具。候选与正式资源由工具命令区分。 | 33 | 364 |

| tools/ci | 仓库卫生、UI/Core 隔离和既有冒烟记录脚本。此报告不运行这些游戏测试。 | 4 | 4 |

| tools/diagnostics | 开发控制台、运行诊断、回放舞台与图像/角色回执工具。 | 6 | 78 |

| tools/godot_architect | 原生编辑器插件、预制件面板、gizmo 与实时建筑叠层。 | 4 | 38 |

| tools/report | 本报告的可复用静态分析与 HTML 生成器。 | 3 | 17 |

| tools/sandbox | 现存作者沙盒与调试覆盖层，不作为本报告的运行测试入口。 | 2 | 9 |

| tools/story | 故事编写、结构导出与聚焦审计；部分 C# 被 CLI 工程显式链接。 | 3 | 47 |

| tools/studio_web | 证明 DAG 与时间线的浏览器工作台。 | 2 | 5 |

| tools/tuning | 角色数值提取、优化、验证与发布流程。 | 1 | 17 |

| tools/verification | 既有移动回归 Godot 场景与脚本。 | 1 | 6 |

| tools/web_building_editor | 浏览器房间编辑器、目录转换与页面补丁；包含随仓第三方 js-yaml。 | 4 | 164 |



## 13. 全项目目录树与资源规模


枚举当前工作树中的普通文件，包含未跟踪文件；排除缓存、构建产物、生成报告、版本控制内部和符号链接目标。二进制美术资源按扩展名统计，不逐个展开名称。


```text

dual_ontology_world_reconstruct/

  .vscode/

  ShareAsset/

    Internal/

      Props/

        ReferenceFacade/

          Corners/

          Cornice/

          Foundation/

          Openings/

          Roof/

          Wall/

        World/

          props/

          rooms/

  assets/

    audio/

      music/

      sfx/

    characters/

      AnimeCast/

      DialogueCast/

      Highlights/

      HighlightsRpg/

      PixelCast/

      UserModels/

      UserModels_backup_original/

      base_body/

      voices/

    data/

      cases/

      content-revisions/

      presentation/

      scenarios/

    environment/

      Audio/

        Music/

        Sfx/

      UIDesk/

        Painterly3D/

      UIPaper/

    external/

      Almendra/

      KenneyNature/

      MaterialSymbols/

      NotoSansSC/

      NotoSerifSC/

      Yozai/

      ZenKurenaido/

      ZhaohuaTypeWriter/

      newtegomin/

      yujisyuku/

    imported/

      kaykit/

        animations/

        characters/

        license/

    localization/

    materials/

      common/

      effects/

      post/

      surfaces/

    meshes/

    packs/

    scenes/

      campaign_rooms/

        components/

        models/

    shaders/

      common/

      effects/

      post/

      surfaces/

    ui/

      manuscript/

      material/

  config/

  core/

    autonomy/

    cognition/

    dv_lang/

    ontology/

    reasoning/

    session_cli/

    simulation/

    spatial/

  docs/

    verification/

  mcp/

    asset_pipeline/

      chapter2_art/

      characters/

    build/

    game/

    spatial/

    verification/

  scenes/

    UIpanel/

      BookUI/

      GUI/

      SaveUI/

      SettingUI/

      TitleUI/

    World/

      Field/

      Maps/

    components/

      architecture/

      characters/

      rendering/

        vendor/

          serj_magnifier/

      shared/

      world/

    main/

    prefabs/

      architecture/

      doors_windows/

      furniture/

      interactive_props/

      lighting_decor/

    rooms/

  tools/

    audio/

    building/

      designs/

      lowpoly_chapters_v3/

        kit/

      prop_recipes/

    ci/

    diagnostics/

    godot_architect/

    report/

    sandbox/

    story/

    studio_web/

    tuning/

    verification/

    web_building_editor/

      rooms_yaml/

      saved_scenes/

```


| 扩展名 | 文件数 |

| --- | --- |

| .import | 1424 |

| .png | 709 |

| .tscn | 689 |

| .glb | 568 |

| .uid | 251 |

| .tres | 153 |

| .gdshader | 88 |

| .gd | 86 |

| .py | 79 |

| .cs | 61 |

| .svg | 52 |

| .ogg | 42 |

| .yaml | 39 |

| .dv | 34 |

| .bin | 32 |

| .jpg | 28 |

| .translation | 28 |

| .md | 25 |

| .json | 24 |

| .gdshaderinc | 14 |

| .txt | 13 |

| .ttf | 11 |

| .csproj | 9 |

| .csv | 7 |

| .wav | 6 |

| .po | 5 |

| .html | 4 |

| (无扩展名) | 3 |

| .mjs | 2 |

| .cfg | 1 |

| .config | 1 |

| .godot | 1 |

| .gz | 1 |

| .js | 1 |

| .sln | 1 |

| .webp | 1 |



根文件：`.gitignore`；`AGENTS.md`；`DualOntologyWorldReconstruct.csproj`；`DuelView.sln`；`MECHANISM_MAP.yaml`；`NuGet.Config`；`goal.md`；`project.godot`


## 14. 完整源码路径索引


共 335 份源码。含第三方库、报告工具、Shader/include 和旧验证代码；列入不表示当前游戏必定加载。这里不重复粘贴原文。


### assets/materials


```text
assets/materials/academy_surface.gdshader
assets/materials/book_leaf_mesh_3d.gdshader
assets/materials/book_spread_page.gdshader
assets/materials/common/occlusion_dither.gdshaderinc
assets/materials/common/surface_metrics.gdshaderinc
assets/materials/common/surface_noise.gdshaderinc
assets/materials/desk_flame.gdshader
assets/materials/desk_glass.gdshader
assets/materials/effects/explored_fog.gdshader
assets/materials/effects/fire.gdshader
assets/materials/effects/memory_dim.gdshader
assets/materials/framework_outline.gdshader
assets/materials/framework_surface.gdshader
assets/materials/lighthouse_beam.gdshader
assets/materials/night_sea.gdshader
assets/materials/night_volcano_sky.gdshader
assets/materials/painterly_surface.gdshader
assets/materials/post/contour.gdshader
assets/materials/room_procedural_surface.gdshader
assets/materials/study_curtain.gdshader
assets/materials/surfaces/aged_metal.gdshader
assets/materials/surfaces/community_grass.gdshader
assets/materials/surfaces/exterior_plaster.gdshader
assets/materials/surfaces/exterior_roof_tiles.gdshader
assets/materials/surfaces/fabric.gdshader
assets/materials/surfaces/foliage.gdshader
assets/materials/surfaces/interior_plank.gdshader
assets/materials/surfaces/interior_stone_floor.gdshader
assets/materials/surfaces/interior_wall.gdshader
assets/materials/surfaces/outdoor_ground.gdshader
assets/materials/surfaces/outdoor_paving.gdshader
assets/materials/surfaces/painted_surface.gdshader
assets/materials/surfaces/parquet.gdshader
assets/materials/surfaces/roof_tiles.gdshader
assets/materials/surfaces/stone.gdshader
assets/materials/surfaces/tile_floor.gdshader
assets/materials/surfaces/water.gdshader
assets/materials/surfaces/wood.gdshader
```


### assets/shaders


```text
assets/shaders/academy_surface.gdshader
assets/shaders/book_leaf_mesh_3d.gdshader
assets/shaders/common/occlusion_dither.gdshaderinc
assets/shaders/common/surface_metrics.gdshaderinc
assets/shaders/common/surface_noise.gdshaderinc
assets/shaders/contour.gdshader
assets/shaders/desk_flame.gdshader
assets/shaders/desk_glass.gdshader
assets/shaders/effects/explored_fog.gdshader
assets/shaders/effects/fire.gdshader
assets/shaders/effects/memory_dim.gdshader
assets/shaders/framework_outline.gdshader
assets/shaders/framework_surface.gdshader
assets/shaders/occlusion_dither.gdshaderinc
assets/shaders/painterly_surface.gdshader
assets/shaders/post/contour.gdshader
assets/shaders/room_procedural_surface.gdshader
assets/shaders/surface_metrics.gdshaderinc
assets/shaders/surface_noise.gdshaderinc
assets/shaders/surfaces/aged_metal.gdshader
assets/shaders/surfaces/community_grass.gdshader
assets/shaders/surfaces/exterior_plaster.gdshader
assets/shaders/surfaces/exterior_roof_tiles.gdshader
assets/shaders/surfaces/fabric.gdshader
assets/shaders/surfaces/foliage.gdshader
assets/shaders/surfaces/interior_plank.gdshader
assets/shaders/surfaces/interior_stone_floor.gdshader
assets/shaders/surfaces/interior_wall.gdshader
assets/shaders/surfaces/outdoor_ground.gdshader
assets/shaders/surfaces/outdoor_paving.gdshader
assets/shaders/surfaces/painted_surface.gdshader
assets/shaders/surfaces/parquet.gdshader
assets/shaders/surfaces/roof_tiles.gdshader
assets/shaders/surfaces/stone.gdshader
assets/shaders/surfaces/tile_floor.gdshader
assets/shaders/surfaces/water.gdshader
assets/shaders/surfaces/wood.gdshader
```


### core/autonomy


```text
core/autonomy/AutonomyDriveSolver.cs
core/autonomy/DecisionScheduler.cs
core/autonomy/DriveCalculator.cs
core/autonomy/GoalPlanning.cs
core/autonomy/Sim4Planner.cs
```


### core/cognition


```text
core/cognition/ActorCognition.cs
core/cognition/Awareness.cs
core/cognition/BinaryEvidence.cs
core/cognition/ConversationMind.cs
core/cognition/Explanations.cs
core/cognition/Perception.cs
core/cognition/PersonalPolicy.cs
```


### core/dv_lang


```text
core/dv_lang/DvCompiler.cs
core/dv_lang/DvDocument.cs
core/dv_lang/DvScriptReader.cs
core/dv_lang/HistoryScriptReader.cs
```


### core/ontology


```text
core/ontology/Arguments.cs
core/ontology/ContentCatalog.cs
core/ontology/Events.cs
core/ontology/PresentationSpec.cs
core/ontology/Primitives.cs
core/ontology/References.cs
core/ontology/Snapshots.cs
core/ontology/State.cs
core/ontology/ValueOperations.cs
```


### core/reasoning


```text
core/reasoning/CaseBranches.cs
core/reasoning/CaseHistory.cs
core/reasoning/CaseProofChecker.cs
core/reasoning/CaseStrategy.cs
core/reasoning/CaseTheory.cs
core/reasoning/NaturalDeduction.cs
```


### core/session_cli


```text
core/session_cli/AuditAndTranscript.cs
core/session_cli/CampaignSessionHost.cs
core/session_cli/ObserverProjection.cs
core/session_cli/Program.cs
core/session_cli/Session.cs
core/session_cli/SessionActionApi.cs
core/session_cli/SessionCaseApi.cs
core/session_cli/SessionReplay.cs
core/session_cli/VerificationSuites.cs
```


### core/simulation


```text
core/simulation/CampaignScenario.cs
core/simulation/CharacterSessionDef.cs
core/simulation/ConversationRuntime.cs
core/simulation/ExecutionEngine.cs
core/simulation/RuntimeContent.cs
core/simulation/RuntimeSession.cs
core/simulation/SimulationClock.cs
core/simulation/WorldRules.cs
```


### core/spatial


```text
core/spatial/NavigationGraph.cs
core/spatial/PortalPolicy.cs
core/spatial/RoomLifecycle.cs
core/spatial/SeatingFit.cs
core/spatial/SlotReservations.cs
```


### mcp/asset_pipeline


```text
mcp/asset_pipeline/chapter2_art/__init__.py
mcp/asset_pipeline/chapter2_art/buildings.py
mcp/asset_pipeline/chapter2_art/nature.py
mcp/asset_pipeline/chapter2_art/oblique.py
mcp/asset_pipeline/chapter2_art/outdoor.py
mcp/asset_pipeline/chapter2_art/painter.py
mcp/asset_pipeline/chapter2_art/props_archive.py
mcp/asset_pipeline/chapter2_art/props_core.py
mcp/asset_pipeline/chapter2_art/props_service.py
mcp/asset_pipeline/chapter2_art/rooms.py
mcp/asset_pipeline/characters/prepare_character.py
mcp/asset_pipeline/characters/prepare_chibi_base.py
mcp/asset_pipeline/characters/prepare_vroid_character.py
mcp/asset_pipeline/characters/render_character.py
mcp/asset_pipeline/characters/skin_e_character.py
```


### mcp/build


```text
mcp/build/build_session.py
mcp/build/dv_toolchain.py
```


### mcp/game


```text
mcp/game/McpControl.gd
mcp/game/ViewportCapture.gd
mcp/game/asset_bundle.py
mcp/game/automation.py
mcp/game/build_cache.py
mcp/game/environment.py
mcp/game/input_client.py
mcp/game/linux_display.py
mcp/game/play.py
mcp/game/run_auto.py
mcp/game/run_environment.py
mcp/game/runtime.py
mcp/game/server.py
```


### mcp/spatial


```text
mcp/spatial/audit_ig01.py
```


### mcp/verification


```text
mcp/verification/cli_report.py
mcp/verification/verify_batch1.py
mcp/verification/verify_batch2.py
mcp/verification/verify_batch3.py
mcp/verification/verify_batch4_and_stage_f.py
```


### scenes/UIpanel


```text
scenes/UIpanel/BookUI/BookUI.gd
scenes/UIpanel/GUI/ActionChoiceGUI.gd
scenes/UIpanel/GUI/ClockRoomGUI.gd
scenes/UIpanel/GUI/DeductionGUI.gd
scenes/UIpanel/GUI/FieldGUI.gd
scenes/UIpanel/GUI/GuidanceGUI.gd
scenes/UIpanel/GUI/PersuasionGUI.gd
scenes/UIpanel/GUI/TaskProgressGUI.gd
scenes/UIpanel/SaveUI/SaveUI.gd
scenes/UIpanel/SettingUI/SettingUI.gd
scenes/UIpanel/TitleUI/TitleUI.gd
```


### scenes/World


```text
scenes/World/Field/ReferenceFacade.gd
```


### scenes/components/architecture


```text
scenes/components/architecture/BuildRoutes.gd
scenes/components/architecture/CircularBlastDoor3D.gd
scenes/components/architecture/CutawayGeometry.cs
scenes/components/architecture/DoorFrameClearance.gd
scenes/components/architecture/ExteriorRoof3D.gd
scenes/components/architecture/GeneratedVisualComponent.gd
scenes/components/architecture/HingedDoor3D.gd
scenes/components/architecture/HingedWindow3D.gd
scenes/components/architecture/PropEntity3D.gd
scenes/components/architecture/RoomContainer3D.gd
scenes/components/architecture/RoomStreamingController.cs
scenes/components/architecture/SmartFloorZone3D.gd
scenes/components/architecture/SmartRailing3D.gd
scenes/components/architecture/SmartStair3D.gd
scenes/components/architecture/SmartWall3D.gd
```


### scenes/components/characters


```text
scenes/components/characters/ActorActionQueue.gd
scenes/components/characters/ActorMotor3D.gd
scenes/components/characters/AutonomousActorExecutor.gd
scenes/components/characters/CampaignRoomHost.gd
scenes/components/characters/CharacterController3D.gd
scenes/components/characters/ConversationAnimationLibrary.gd
scenes/components/characters/ConversationController3D.gd
scenes/components/characters/ImportedCharacter.gd
scenes/components/characters/KayKitAnimationLibrary.gd
scenes/components/characters/PhysicalQueryBudget.gd
scenes/components/characters/ReachTarget3D.gd
scenes/components/characters/RoomTransition.gd
scenes/components/characters/SeatPoseModifier3D.gd
scenes/components/characters/SeatPostureDriver3D.gd
scenes/components/characters/SimsBehaviorDirector.gd
scenes/components/characters/SpatialParticipants3D.gd
scenes/components/characters/StoryActor.gd
scenes/components/characters/StoryBody3D.gd
```


### scenes/components/rendering


```text
scenes/components/rendering/ArchitecturalSurface.gdshader
scenes/components/rendering/ArchitecturalSurface.gdshaderinc
scenes/components/rendering/AtmosphereRig.gd
scenes/components/rendering/AuthoringGrass.gdshader
scenes/components/rendering/AuthoringGround.gdshader
scenes/components/rendering/CameraOcclusion.gd
scenes/components/rendering/ChamferGeometry.gd
scenes/components/rendering/ChibiFigurineSurface.gdshader
scenes/components/rendering/ClusterCanopyCards.gdshader
scenes/components/rendering/ClusterCanopySolid.gdshader
scenes/components/rendering/CourtyardPavingCandidate.gdshader
scenes/components/rendering/CutawayLighting.gd
scenes/components/rendering/FaRuFoliage.gdshader
scenes/components/rendering/FurnitureCandidateSurface.gdshader
scenes/components/rendering/InteriorFinish.gd
scenes/components/rendering/InteriorFinish.gdshader
scenes/components/rendering/JHellFoliageCutout.gdshader
scenes/components/rendering/JHellFoliageScale.gdshader
scenes/components/rendering/MaterialLibrary.gd
scenes/components/rendering/ObserverEnvironment3D.gd
scenes/components/rendering/PersistentMultiMesh.gd
scenes/components/rendering/PlayerViewTrace.gd
scenes/components/rendering/RenderDetailController.gd
scenes/components/rendering/RpgCameraRig.gd
scenes/components/rendering/RpgPixelView.gd
scenes/components/rendering/RpgRenderProfile.gd
scenes/components/rendering/RpgRenderer.gd
scenes/components/rendering/SharedPresentationContext3D.gd
scenes/components/rendering/StylizedSurface.gdshader
scenes/components/rendering/TopDownCamera3D.gd
scenes/components/rendering/VegetationShared.gdshaderinc
scenes/components/rendering/VegetationSurface.gdshader
scenes/components/rendering/VegetationWind.gdshaderinc
scenes/components/rendering/WorldTimeLighting.gd
scenes/components/rendering/astra_canopy_surface.gdshader
scenes/components/rendering/astra_faceted_surface.gdshader
scenes/components/rendering/astra_painted_surface.gdshader
scenes/components/rendering/book_page_stack.gdshader
scenes/components/rendering/magnifier_lens.gdshader
scenes/components/rendering/manuscript_mask.gdshader
scenes/components/rendering/manuscript_ribbon.gdshader
scenes/components/rendering/manuscript_surface.gdshaderinc
scenes/components/rendering/room_visibility.gdshaderinc
scenes/components/rendering/vendor/serj_magnifier/original.gdshader
```


### scenes/components/shared


```text
scenes/components/shared/GameAudio.gd
scenes/components/shared/GameSettings.gd
scenes/components/shared/HostLifecycle.gd
scenes/components/shared/LocaleSettings.gd
scenes/components/shared/MaterialControls.gd
scenes/components/shared/MusicContext.gd
scenes/components/shared/Sound.gd
```


### scenes/components/world


```text
scenes/components/world/BookLeafTurn3D.gd
scenes/components/world/BookPageStack3D.gd
scenes/components/world/BookSurfaceDetail3D.gd
scenes/components/world/DesignCoordinates.gd
scenes/components/world/PhysicsLayers.gd
scenes/components/world/StudyDeskStage3D.gd
```


### scenes/main


```text
scenes/main/CasebookPersistence.cs
scenes/main/DvPresentationContent.cs
scenes/main/GodotHostValues.cs
scenes/main/Main.gd
scenes/main/SessionBridge.cs
```


### tools/audio


```text
tools/audio/synth_sfx.py
```


### tools/building


```text
tools/building/SeatingFit.cs
tools/building/compose_courtyard_rooms.py
tools/building/lowpoly_chapters_v3/kit/__init__.py
tools/building/lowpoly_chapters_v3/kit/architecture.py
tools/building/lowpoly_chapters_v3/kit/core.py
tools/building/lowpoly_chapters_v3/kit/decor.py
tools/building/lowpoly_chapters_v3/kit/export_godot.py
tools/building/lowpoly_chapters_v3/kit/flooring.py
tools/building/lowpoly_chapters_v3/kit/furniture.py
tools/building/lowpoly_chapters_v3/kit/grand_hall.py
tools/building/lowpoly_chapters_v3/kit/hall_architecture.py
tools/building/lowpoly_chapters_v3/kit/hall_scene.py
tools/building/lowpoly_chapters_v3/kit/industrial_props.py
tools/building/lowpoly_chapters_v3/kit/industrial_scenes.py
tools/building/lowpoly_chapters_v3/kit/lighting.py
tools/building/lowpoly_chapters_v3/kit/openings.py
tools/building/lowpoly_chapters_v3/kit/outdoor_props.py
tools/building/lowpoly_chapters_v3/kit/outdoor_scenes.py
tools/building/lowpoly_chapters_v3/kit/paths.py
tools/building/lowpoly_chapters_v3/kit/public_props.py
tools/building/lowpoly_chapters_v3/kit/public_scenes.py
tools/building/lowpoly_chapters_v3/kit/rendering.py
tools/building/lowpoly_chapters_v3/kit/residential_props.py
tools/building/lowpoly_chapters_v3/kit/residential_scenes.py
tools/building/lowpoly_chapters_v3/kit/scene_common.py
tools/building/lowpoly_chapters_v3/kit/scenes.py
tools/building/lowpoly_chapters_v3/kit/showcase.py
tools/building/lowpoly_chapters_v3/kit/special_props.py
tools/building/lowpoly_chapters_v3/kit/stair_exact.py
tools/building/lowpoly_chapters_v3/kit/variants.py
tools/building/pack_campaign_assets.py
tools/building/prop_workshop.py
tools/building/publish_campaign_rooms.py
```


### tools/ci


```text
tools/ci/run_godot_smoke_record.py
tools/ci/verify_building_editor.mjs
tools/ci/verify_repo_hygiene.py
tools/ci/verify_ui_core_isolation.py
```


### tools/diagnostics


```text
tools/diagnostics/DevConsole.gd
tools/diagnostics/ReplayStage.gd
tools/diagnostics/RuntimeDiagnostics.gd
tools/diagnostics/character_receipts.py
tools/diagnostics/compare_receipt_images.py
tools/diagnostics/stage1_receipts.py
```


### tools/godot_architect


```text
tools/godot_architect/ArchitectGizmo3D.gd
tools/godot_architect/ArchitectPlugin.gd
tools/godot_architect/LiveBuilderOverlay.gd
tools/godot_architect/PrefabPaletteDock.gd
```


### tools/report


```text
tools/report/generate_code_analysis_report.py
tools/report/report.html
tools/report/validate_report.mjs
```


### tools/sandbox


```text
tools/sandbox/Day1Harness.gd
tools/sandbox/DebugOverlay3D.gd
```


### tools/story


```text
tools/story/ScenarioAuthoring.cs
tools/story/author.py
tools/story/focus_audit.py
```


### tools/studio_web


```text
tools/studio_web/proof_dag.html
tools/studio_web/timeline_gantt.html
```


### tools/tuning


```text
tools/tuning/character_tuning.py
```


### tools/verification


```text
tools/verification/MovementRegression.gd
```


### tools/web_building_editor


```text
tools/web_building_editor/convert_py_to_catalog.py
tools/web_building_editor/index.html
tools/web_building_editor/js-yaml.min.js
tools/web_building_editor/patch_index_html.py
```


## 15. 场景、DV、资源配置与工程完整索引


共 926 份，范围为 .tscn/.tres/.dv/.csproj/.godot/.yaml/.sln。图片、音频、网格、导入旁文件与普通 JSON/CSV 等不属于此索引范围，其目录和扩展名规模见第 13 节。


### (根目录)


```text
DualOntologyWorldReconstruct.csproj
DuelView.sln
MECHANISM_MAP.yaml
project.godot
```


### ShareAsset


```text
ShareAsset/Internal/Props/ReferenceFacade/ReferenceFacade.tres
```


### assets


```text
assets/audio/default_bus_layout.tres
assets/characters/AnimeCast/archaeologist.tres
assets/characters/AnimeCast/archaeologist_portrait.tres
assets/characters/AnimeCast/architect.tres
assets/characters/AnimeCast/architect_portrait.tres
assets/characters/AnimeCast/chef.tres
assets/characters/AnimeCast/chef_portrait.tres
assets/characters/AnimeCast/chemist.tres
assets/characters/AnimeCast/chemist_portrait.tres
assets/characters/AnimeCast/chessplayer.tres
assets/characters/AnimeCast/chessplayer_portrait.tres
assets/characters/AnimeCast/dancer.tres
assets/characters/AnimeCast/dancer_portrait.tres
assets/characters/AnimeCast/detective.tres
assets/characters/AnimeCast/detective_portrait.tres
assets/characters/AnimeCast/doctor.tres
assets/characters/AnimeCast/doctor_portrait.tres
assets/characters/AnimeCast/engineer.tres
assets/characters/AnimeCast/engineer_portrait.tres
assets/characters/AnimeCast/lawyer.tres
assets/characters/AnimeCast/lawyer_portrait.tres
assets/characters/AnimeCast/marksman.tres
assets/characters/AnimeCast/marksman_portrait.tres
assets/characters/AnimeCast/musician.tres
assets/characters/AnimeCast/musician_portrait.tres
assets/characters/AnimeCast/naturalist.tres
assets/characters/AnimeCast/naturalist_portrait.tres
assets/characters/AnimeCast/painter.tres
assets/characters/AnimeCast/painter_portrait.tres
assets/characters/AnimeCast/poet.tres
assets/characters/AnimeCast/poet_portrait.tres
assets/characters/AnimeCast/thief.tres
assets/characters/AnimeCast/thief_portrait.tres
assets/characters/DialogueCast/archaeologist_angry.tres
assets/characters/DialogueCast/archaeologist_calm.tres
assets/characters/DialogueCast/archaeologist_hooked.tres
assets/characters/DialogueCast/archaeologist_thoughtful.tres
assets/characters/DialogueCast/archaeologist_warm.tres
assets/characters/DialogueCast/archaeologist_worried.tres
assets/characters/DialogueCast/architect_angry.tres
assets/characters/DialogueCast/architect_calm.tres
assets/characters/DialogueCast/architect_hooked.tres
assets/characters/DialogueCast/architect_thoughtful.tres
assets/characters/DialogueCast/architect_warm.tres
assets/characters/DialogueCast/architect_worried.tres
assets/characters/DialogueCast/chef_angry.tres
assets/characters/DialogueCast/chef_calm.tres
assets/characters/DialogueCast/chef_hooked.tres
assets/characters/DialogueCast/chef_thoughtful.tres
assets/characters/DialogueCast/chef_warm.tres
assets/characters/DialogueCast/chef_worried.tres
assets/characters/DialogueCast/chemist_angry.tres
assets/characters/DialogueCast/chemist_calm.tres
assets/characters/DialogueCast/chemist_hooked.tres
assets/characters/DialogueCast/chemist_thoughtful.tres
assets/characters/DialogueCast/chemist_warm.tres
assets/characters/DialogueCast/chemist_worried.tres
assets/characters/DialogueCast/chessplayer_angry.tres
assets/characters/DialogueCast/chessplayer_calm.tres
assets/characters/DialogueCast/chessplayer_hooked.tres
assets/characters/DialogueCast/chessplayer_thoughtful.tres
assets/characters/DialogueCast/chessplayer_warm.tres
assets/characters/DialogueCast/chessplayer_worried.tres
assets/characters/DialogueCast/dancer_angry.tres
assets/characters/DialogueCast/dancer_calm.tres
assets/characters/DialogueCast/dancer_hooked.tres
assets/characters/DialogueCast/dancer_thoughtful.tres
assets/characters/DialogueCast/dancer_warm.tres
assets/characters/DialogueCast/dancer_worried.tres
assets/characters/DialogueCast/detective_angry.tres
assets/characters/DialogueCast/detective_calm.tres
assets/characters/DialogueCast/detective_hooked.tres
assets/characters/DialogueCast/detective_thoughtful.tres
assets/characters/DialogueCast/detective_warm.tres
assets/characters/DialogueCast/detective_worried.tres
assets/characters/DialogueCast/doctor_angry.tres
assets/characters/DialogueCast/doctor_calm.tres
assets/characters/DialogueCast/doctor_hooked.tres
assets/characters/DialogueCast/doctor_thoughtful.tres
assets/characters/DialogueCast/doctor_warm.tres
assets/characters/DialogueCast/doctor_worried.tres
assets/characters/DialogueCast/engineer_angry.tres
assets/characters/DialogueCast/engineer_calm.tres
assets/characters/DialogueCast/engineer_hooked.tres
assets/characters/DialogueCast/engineer_thoughtful.tres
assets/characters/DialogueCast/engineer_warm.tres
assets/characters/DialogueCast/engineer_worried.tres
assets/characters/DialogueCast/lawyer_angry.tres
assets/characters/DialogueCast/lawyer_calm.tres
assets/characters/DialogueCast/lawyer_hooked.tres
assets/characters/DialogueCast/lawyer_thoughtful.tres
assets/characters/DialogueCast/lawyer_warm.tres
assets/characters/DialogueCast/lawyer_worried.tres
assets/characters/DialogueCast/marksman_angry.tres
assets/characters/DialogueCast/marksman_calm.tres
assets/characters/DialogueCast/marksman_hooked.tres
assets/characters/DialogueCast/marksman_thoughtful.tres
assets/characters/DialogueCast/marksman_warm.tres
assets/characters/DialogueCast/marksman_worried.tres
assets/characters/DialogueCast/musician_angry.tres
assets/characters/DialogueCast/musician_calm.tres
assets/characters/DialogueCast/musician_hooked.tres
assets/characters/DialogueCast/musician_thoughtful.tres
assets/characters/DialogueCast/musician_warm.tres
assets/characters/DialogueCast/musician_worried.tres
assets/characters/DialogueCast/naturalist_angry.tres
assets/characters/DialogueCast/naturalist_calm.tres
assets/characters/DialogueCast/naturalist_hooked.tres
assets/characters/DialogueCast/naturalist_thoughtful.tres
assets/characters/DialogueCast/naturalist_warm.tres
assets/characters/DialogueCast/naturalist_worried.tres
assets/characters/DialogueCast/painter_angry.tres
assets/characters/DialogueCast/painter_calm.tres
assets/characters/DialogueCast/painter_hooked.tres
assets/characters/DialogueCast/painter_thoughtful.tres
assets/characters/DialogueCast/painter_warm.tres
assets/characters/DialogueCast/painter_worried.tres
assets/characters/DialogueCast/poet_angry.tres
assets/characters/DialogueCast/poet_calm.tres
assets/characters/DialogueCast/poet_hooked.tres
assets/characters/DialogueCast/poet_thoughtful.tres
assets/characters/DialogueCast/poet_warm.tres
assets/characters/DialogueCast/poet_worried.tres
assets/characters/DialogueCast/thief_angry.tres
assets/characters/DialogueCast/thief_calm.tres
assets/characters/DialogueCast/thief_hooked.tres
assets/characters/DialogueCast/thief_thoughtful.tres
assets/characters/DialogueCast/thief_warm.tres
assets/characters/DialogueCast/thief_worried.tres
assets/characters/PixelCast/archaeologist.tres
assets/characters/PixelCast/architect.tres
assets/characters/PixelCast/chef.tres
assets/characters/PixelCast/chemist.tres
assets/characters/PixelCast/chessplayer.tres
assets/characters/PixelCast/dancer.tres
assets/characters/PixelCast/detective.tres
assets/characters/PixelCast/doctor.tres
assets/characters/PixelCast/engineer.tres
assets/characters/PixelCast/lawyer.tres
assets/characters/PixelCast/marksman.tres
assets/characters/PixelCast/musician.tres
assets/characters/PixelCast/naturalist.tres
assets/characters/PixelCast/painter.tres
assets/characters/PixelCast/poet.tres
assets/characters/PixelCast/thief.tres
assets/data/98-runtime-session.data.dv
assets/data/cases/r3_disaster_history.dv
assets/data/cases/r3_npc_strategy.dv
assets/data/cases/r3_opening_history.dv
assets/data/character_art.dv
assets/data/presentation.dv
assets/data/presentation/campaign_rooms.dv
assets/data/scenarios/campaign.dv
assets/data/scenarios/daily-life.dv
assets/data/scenarios/day1-morning.dv
assets/data/scenarios/day2.dv
assets/data/scenarios/day3.dv
assets/data/scenarios/day4.dv
assets/data/scenarios/day5.dv
assets/data/scenarios/detective-planning.dv
assets/data/scenarios/life-demon.dv
assets/data/scenarios/life-detective-huo.dv
assets/data/scenarios/life-elena-moran.dv
assets/data/scenarios/life-lucian-gu.dv
assets/data/scenarios/life-margaret-mora.dv
assets/data/scenarios/life-quer-ward.dv
assets/data/scenarios/life-selma-ruan.dv
assets/data/scenarios/life-shen-su.dv
assets/data/scenarios/life-veronica-bertrand.dv
assets/data/scenarios/open-day5-detective-huo.dv
assets/data/scenarios/open-day5-elena-moran.dv
assets/data/scenarios/open-day5-lucian-gu.dv
assets/data/scenarios/open-day5-margaret-mora.dv
assets/data/scenarios/open-day5-quer-ward.dv
assets/data/scenarios/open-day5-selma-ruan.dv
assets/data/scenarios/open-day5-shen-su.dv
assets/data/scenarios/open-day5-veronica-bertrand.dv
assets/data/scenarios/social-schemes.dv
assets/external/Almendra/Almendra-Bold.tres
assets/external/Almendra/Almendra-Regular.tres
assets/imported/kaykit/KayKitAnimationLibrary.tres
assets/scenes/campaign_rooms/components/00fc897531acacd91fc294e6149da49b5f4189212987d8036da2f3b4c4e493de.tscn
assets/scenes/campaign_rooms/components/01710e481c4878c4549e94a5b7291bbf6db2d19af531045bc59fef9a54f1553b.tscn
assets/scenes/campaign_rooms/components/02e0d09afe30f873f20d0968cbc9c9ba55a0dd8caa9881823094da6bc72c04d3.tscn
assets/scenes/campaign_rooms/components/033114f397fa72dab54f17f32e50689da7adcd2c3bfb3877835e443a87bf12ba.tscn
assets/scenes/campaign_rooms/components/0354e1875e8466c2a79470c9a6664f5b5deb140843f0a3d258302064573545ff.tscn
assets/scenes/campaign_rooms/components/037f7bc00461a8b98e93d7c3875537df8cd4065a402c1b0ffc1d039e34880279.tscn
assets/scenes/campaign_rooms/components/04fe881dade0e3be5dffb63d8ad61b5b71e0b44d45f4630a7d37d97d9ff2e8ca.tscn
assets/scenes/campaign_rooms/components/05c657aa1a3cac923c3112b28559a0d11e512a9e59fbd0c0dd5a0eb085e7cc14.tscn
assets/scenes/campaign_rooms/components/05eccb5ce2d488640e807d8b50aa4db9bee59a7e1a32bd6a1ac75438c18bd280.tscn
assets/scenes/campaign_rooms/components/073f42e67a010e3667bb9b9923bb7fcb1bd0e382ad50d7778e5b49f0848d3c95.tscn
assets/scenes/campaign_rooms/components/07f4a8ee8d7c7c7009404d6646cba26b7a5ce6e1359e583f517a6cb4cbcbd114.tscn
assets/scenes/campaign_rooms/components/085e25276e05496fb34614fe73186853a097531c3e4636ce96711f479993650b.tscn
assets/scenes/campaign_rooms/components/0978410dfb4f750f6df905231dd53f42a2bdb4ea9be8c97f87202461f4d6b168.tscn
assets/scenes/campaign_rooms/components/0b3ff136e6dadbe6aee7f344e139b2ad90811a632db35ff98b417f6b5c36542a.tscn
assets/scenes/campaign_rooms/components/0b43719da4385cc856e4e449c629debc700b80eab929d4c1832f81bce0fba446.tscn
assets/scenes/campaign_rooms/components/0b5a0f6fe62988914715882422442fcde72b2b0b7c53038b03441235f142a3a7.tscn
assets/scenes/campaign_rooms/components/0c23804d3828fae1144899fbe8cfe2df707b3acde6529fb51350c67b2d9f41cc.tscn
assets/scenes/campaign_rooms/components/0d68f30dee6da8851adc512e5f5eed3a2e171ab33d992234aa7128b502be690b.tscn
assets/scenes/campaign_rooms/components/0eb1def38f988a1e8d607ca3c14afb20c57422df34b1c9e4f9881e98d5b0f7cf.tscn
assets/scenes/campaign_rooms/components/0ee04e4f7d4469eb9388b7cea747680c57244d7d2039421041bb830cd3578926.tscn
assets/scenes/campaign_rooms/components/0f97894928b270a47f84ebe72d93f9765563971bb078a7ef4acf325ff954484c.tscn
assets/scenes/campaign_rooms/components/0ff5f6c84ad018810d97401a90f19178f39739b403a88817d86d486a6a44588a.tscn
assets/scenes/campaign_rooms/components/1058fbf9b729260de6686ad8ec12cd0b24e2286faaa12ed32e684e33d3ade1f0.tscn
assets/scenes/campaign_rooms/components/116ef1018c5fb662b0eadb3924f85f15ed8e981ca7712f6669594f44f55e99ec.tscn
assets/scenes/campaign_rooms/components/11b59630843cfd815a0226383f5b7190991adf66f0134b07be10a4215ff2197f.tscn
assets/scenes/campaign_rooms/components/14c0a0f859e7eb5017d7106fb9ed132a5134a1d468c741b96c14d4c3768e7b6b.tscn
assets/scenes/campaign_rooms/components/150cf7da87d3b8c8f1ffea5c509538c310d5f8536d037495236e585ec8948079.tscn
assets/scenes/campaign_rooms/components/156e2ab1af1f7ace64a7ed728028c6becb8340b68cc3df4cf00fc70fa037966d.tscn
assets/scenes/campaign_rooms/components/15c7888c85cd5bfb150ccb90dc67323bd5101021332f65ee6eed16db0708ca4e.tscn
assets/scenes/campaign_rooms/components/15fb91e389a5aaf38ab949d99ff38a50aff13d85fb6f1a62885e215fa72c9e1c.tscn
assets/scenes/campaign_rooms/components/1766a448965332b1d2175dd704d28a57e547e1cf84a7dc563caace026bdda80b.tscn
assets/scenes/campaign_rooms/components/17733630382ceb5773ec1882511eaca04a87571a20cae23281dfe7c082f0eacb.tscn
assets/scenes/campaign_rooms/components/18e0aab1decbc0fd5fe3fc32e91a561620215be2e15c5e8aabe6508946160647.tscn
assets/scenes/campaign_rooms/components/19506c9be6bd4cd8ed0e1253285bd3df751127f1f897cf4fa4fa0a08613e3986.tscn
assets/scenes/campaign_rooms/components/19ff404ceca595d23706cab470542d5ae89f797ac03faae8d50d4f36c59307c1.tscn
assets/scenes/campaign_rooms/components/1a6176f3d6edd0f6112a5e7f058a704de8e38ba9958a39a46022141936836717.tscn
assets/scenes/campaign_rooms/components/1abb277aad01ba839ce5e6296fd560c6d02b8e8ba3692aef72380d8435ee20bf.tscn
assets/scenes/campaign_rooms/components/1b7c67fa02f56498da915b2f6d049978a2f6dcead08b0d7ec9e79f73b7e682d1.tscn
assets/scenes/campaign_rooms/components/1e76619d718a8db83847ab3c3d32140a4fc206eafacab43969ad74bc82bb8b29.tscn
assets/scenes/campaign_rooms/components/1f458eb6c2c27d3f8c31e16506c07d42096a285c4cf04fcafae71ea6644ff4e4.tscn
assets/scenes/campaign_rooms/components/1f48cae9eaaf5f725afbe8fef4e3c65f576dbc4c7c5f7d2da62bac405cd997ef.tscn
assets/scenes/campaign_rooms/components/201218e15c4fbab87acff3aeb44386ec0b489df926c653570677bf0680413ba8.tscn
assets/scenes/campaign_rooms/components/222193b8af7fb28d2cad0a28d3fe4f0a5e8e942189aad52eb89288e894b6aa2a.tscn
assets/scenes/campaign_rooms/components/223c243ae58f572bb1297111f869ef35c294d1105805b3326c3a66bd22efdc28.tscn
assets/scenes/campaign_rooms/components/229267856bbb2b01bd26238f01d8e6cbe3571e6d28b9102337f79628bbd54ed1.tscn
assets/scenes/campaign_rooms/components/22acc13707beae9ff8d65e9b94258c90fcd446aa462e44e2b4f56cd5f4620f7b.tscn
assets/scenes/campaign_rooms/components/23a21fc2a4528835377abbfeded1edae65c11f4447a491e8b670b31790bc75f3.tscn
assets/scenes/campaign_rooms/components/23f647d86bb8667e0a5c80affc280a2db67ca7a7b5f57ccd42cee502cb8bee9e.tscn
assets/scenes/campaign_rooms/components/248bdc8f6d9622e2d31b362e624cc27d51b31a006d42d0f35dd54964c9ce4f80.tscn
assets/scenes/campaign_rooms/components/2558e57ad3524325557b70076ba63e5fa9f96029529a4927bd666a003789102a.tscn
assets/scenes/campaign_rooms/components/25bf661ad0efae780197bb2397363c0ad00c1e72b82c75abc4f80f1387c95d56.tscn
assets/scenes/campaign_rooms/components/25d9812b1a81b5177cbdfce82aed803dd0bcb14fc750fa30f3f0c25623838586.tscn
assets/scenes/campaign_rooms/components/26ceb6bcab49859ad8244142a94c66cd70e7a3a48c99bd289d0d9b2ff96db5e9.tscn
assets/scenes/campaign_rooms/components/295e27f22ca13a523a9ddb4d5fd5c0b67fb4209a574b64a031a1b9be90a26a2f.tscn
assets/scenes/campaign_rooms/components/29c1d4cc1a347608c8b3e5bd31ff7e510bdce5c5e3fe7080edb6141d588ed9dc.tscn
assets/scenes/campaign_rooms/components/2b567029861972e31cecc4eb568c6ae97a3ddb05ccf24a796c72d218345178ab.tscn
assets/scenes/campaign_rooms/components/2bea1d136bb2c31bcce0a946e84710167279910c64d5b3bf2a065f5b0da31faf.tscn
assets/scenes/campaign_rooms/components/2c9017f3f7c204e4f1c8dd8dc7996d2339830ee424a4c2a2b44e5d382392401b.tscn
assets/scenes/campaign_rooms/components/2cf5d18c20031c3ed8ddcfc80a24b14ac6bff4e9b86af1df6556e62b0f38c0a1.tscn
assets/scenes/campaign_rooms/components/2e629e445428bbee0acd0972857e4ad179014666cfc93f2c657de8fb6442a1dc.tscn
assets/scenes/campaign_rooms/components/2fb18dc023499fc9050cbe81cf63ada294b74f3ee28660fad1bc9780bc97b5ae.tscn
assets/scenes/campaign_rooms/components/3057ca33135512a29091e8138b7f727d47e628e6f5e28800c746740a8e9dfdc6.tscn
assets/scenes/campaign_rooms/components/31324cf01e7daf9f07b5bb086c7476d5aca465747c1b9c07b6d7464be4ccd65d.tscn
assets/scenes/campaign_rooms/components/313ad5d638c5ac5f3990e8e5ce34f15f67262a48633837eb15b0a97b15cf5ad1.tscn
assets/scenes/campaign_rooms/components/31cbf82a716add1d74fffc1e93042b103c98da8d0cd3f9758d63e5d1791703ac.tscn
assets/scenes/campaign_rooms/components/31d2752593fe330560ba728115598ebfbdd9c6d268e0819996d09ebd75af7221.tscn
assets/scenes/campaign_rooms/components/31f7d1a2f6ebdbe84b04c71ca8a3681fe8b1b22a897f1a51cce806f55f33fda5.tscn
assets/scenes/campaign_rooms/components/351dc4bc2e55683ddfe990c887fdeb58fe225c1bc2cf42dd317137bb2aca5e6e.tscn
assets/scenes/campaign_rooms/components/3ac631386fd3d442968b5650ad55d8b7141ad72c957f4f05ccc37805959c7ca8.tscn
assets/scenes/campaign_rooms/components/3b382097c501deeb00c714623912844c84832b4aec903ab025023d7b0b2d1367.tscn
assets/scenes/campaign_rooms/components/3c4e4c856eb006eeaa70b8d3b3737fab8f551d5d34f02898376ada8131025e44.tscn
assets/scenes/campaign_rooms/components/3c762f6862ee0385fdd5a95fd2f7cb822b019353d385350348bb3ccdc617b3d9.tscn
assets/scenes/campaign_rooms/components/3cb6340253da030ea2279796462df8ff3f114dcae3880be93277c242ffc39dff.tscn
assets/scenes/campaign_rooms/components/3d8b1d16f15d9ce44f930aa412ea275d021665045d850f8b3e81086b7fe57172.tscn
assets/scenes/campaign_rooms/components/408949c39462f3a248bd0eda938b039593e6e5c99eeae442a17f66fdce373b5c.tscn
assets/scenes/campaign_rooms/components/41c5739b6c7ea0a049280d7f3567e5ca3110264d5f8dab08239e273f61d3e613.tscn
assets/scenes/campaign_rooms/components/4240c46939d7a5700967bc1ffa9c657456dbe65509ddb2943ac45986beb7e295.tscn
assets/scenes/campaign_rooms/components/4299c2c2bd8bcaf77eee0aa92d1bda6e0b6e481cf16a681bae374f54fa51bcdc.tscn
assets/scenes/campaign_rooms/components/431c072f4a520d8b7826a11a8a4f52684ca8be739bf48bdf3f13bf13f9650384.tscn
assets/scenes/campaign_rooms/components/434e47637f04de30ab4aded3806db06d646f42cc24200f38db0819af3cce7328.tscn
assets/scenes/campaign_rooms/components/43d39bda9b01f27bd5923f1a1bb06933487f31f57c08121afecb6f0b45ceafad.tscn
assets/scenes/campaign_rooms/components/46044236ed49158584221c84bd3514f34c4630723556587ba00f0b964e81f4db.tscn
assets/scenes/campaign_rooms/components/47a67ebbe44a4882c069fc472860273f16db6788d036d19d232a65a576f8d0d1.tscn
assets/scenes/campaign_rooms/components/47cc4b6d8c23770b6e2219b9e0e8cd4ff06992dda1063c62171c16d9acf8da40.tscn
assets/scenes/campaign_rooms/components/47eb78a7e585409ab35b245a95a7536e488aa9e2746c34b2d647d9daccd38226.tscn
assets/scenes/campaign_rooms/components/490271c0a07394e08576bb91a44b73e558b208f368d70b1443919ff884370f09.tscn
assets/scenes/campaign_rooms/components/491c5cc3c95d47959514022099c00511830c65cf123464603fc7d3ef841a464b.tscn
assets/scenes/campaign_rooms/components/4a4e435144cd76eec637c6e8590b76d54e4c114a5fb24668758681ed81d18afb.tscn
assets/scenes/campaign_rooms/components/4ae02f100192b384c6dd67c0ab61a883ab6cbb3d5de31e968093eab8877c34c7.tscn
assets/scenes/campaign_rooms/components/4c3b978430a47acd47f4f5cf23250eec27a11e51344500b866a1f82fa1db1331.tscn
assets/scenes/campaign_rooms/components/4cc579c153b79997f43993968a1158a8c832ca7e0a78ea1d00da2b329e46bbc7.tscn
assets/scenes/campaign_rooms/components/4d563d9877d3e82c9acf2dc90fd6de153e91334f6cb201245560703d5a531e6e.tscn
assets/scenes/campaign_rooms/components/4e40a588ab8d7162a2102c68395c79c45bcf2aa1b89c2d3d25529fb63dc6d40c.tscn
assets/scenes/campaign_rooms/components/4eaec6bc8304740b7318d716bf048df72f74a38dcb448ed21e1f1c0ac1feb0ad.tscn
assets/scenes/campaign_rooms/components/507ece2ac2d7b8f9c5831befedf407c8e44f0afdc618ba3094a9f3d690efd7a7.tscn
assets/scenes/campaign_rooms/components/510575b13aa9464f2a108a4376005079a9573d3dd342d2608add4a75edf66afd.tscn
assets/scenes/campaign_rooms/components/513026fa7e4995d9572a780442a13d5a7008ee6c81f1e72fe749038451e40472.tscn
assets/scenes/campaign_rooms/components/51695f14de1ee3199b7ce39e571ca36af37551e2a0870e3f19f4ed07a6004888.tscn
assets/scenes/campaign_rooms/components/519ac55898fca4e634b926b280865ddb8e6db9f86931ff8c48525157d9933dba.tscn
assets/scenes/campaign_rooms/components/52c6e8cac957f7d0a560baa60d827e239c5dd01829cd535f93d3743c5f81e59d.tscn
assets/scenes/campaign_rooms/components/53cd983e1fccb0ee088771c786b04ac8d7dfa3d9f883d6a91e490ea8a88df06d.tscn
assets/scenes/campaign_rooms/components/54bc268bccfdb308f7853c6d9fac11ef79edd32222a4bbc620accade8e74350c.tscn
assets/scenes/campaign_rooms/components/55d4c36f6cc81c29576d73baee40a730e238f6ee4b02413308eee6ae598d8d14.tscn
assets/scenes/campaign_rooms/components/5880817874c619b798c6e7e006b9eb97fa3bc021a25e2afdc4d001b9021ed13e.tscn
assets/scenes/campaign_rooms/components/590a0ae8750c838f5576340fe7796c0dedc639c44a508f3fae6893448dfda542.tscn
assets/scenes/campaign_rooms/components/5aff55230c3c5176d7955196d8f9b425f57940254cb5f4c881b7607762b46600.tscn
assets/scenes/campaign_rooms/components/5b7e1ee4dbcf5917eb2da386fba3f29b8f70eaec837dbb2fd9208a84297f2fa4.tscn
assets/scenes/campaign_rooms/components/5d03a1dbbbe24ebf1f5c9a1ea157720908af91df9e5662f119ec64a2439b5a3d.tscn
assets/scenes/campaign_rooms/components/5d46bd49293e16d94a2679f1a73a7ceb12534939abde14dbc39264fec90600f5.tscn
assets/scenes/campaign_rooms/components/5f25d39d59ef48c6e171874003b948d03ef5398ac9fcb6241c5314a387aa760d.tscn
assets/scenes/campaign_rooms/components/5fa6fe0267ed4f41cf2708e259dafac751081d238ca75f629d9a0cd1e6f1b258.tscn
assets/scenes/campaign_rooms/components/617f11374b20e7c7e288a017c83af9c4fdaab80cf2c3b11fe093b54157819744.tscn
assets/scenes/campaign_rooms/components/61d62e7fafc2472a458fb8de230c75b7cc978cecb8eb9d4037ac1e436a35588d.tscn
assets/scenes/campaign_rooms/components/62bb390ed625796b31452a3b17757906d853eb8ef197080d059db7352f48a8bb.tscn
assets/scenes/campaign_rooms/components/63dba467dacc0a616a8cb29c4e19ccdce3cfb7c63c61e768afe7d69b9fc62b92.tscn
assets/scenes/campaign_rooms/components/647332e625df22f170093416c0bee7543d783b0390dee8740a45fbb475611c48.tscn
assets/scenes/campaign_rooms/components/669b10e04b87cef5ef619ac81e0a1145818adc4df008c1fec5166525e45603e5.tscn
assets/scenes/campaign_rooms/components/68198bb5a661eaec48c9c6384464384c0e14ff518262daf05d2339f1d5b4473b.tscn
assets/scenes/campaign_rooms/components/681e39360cbea937f3024519bd6e45aee425d7cf34806a93ec50b111b0aed608.tscn
assets/scenes/campaign_rooms/components/69ef175d54f1a5c103d9da9d2c0b92f4b2734d59d3798ad6b3069f1f192c42ec.tscn
assets/scenes/campaign_rooms/components/6a674c217037611edf6c74e0697e7a1f514154f0189fea7a9869ba46380108f5.tscn
assets/scenes/campaign_rooms/components/6dfc5861a0059193071bb8aaf3b8a93adef4826d306b8c9992fee8b22a0a2239.tscn
assets/scenes/campaign_rooms/components/6ec8ff173809010e8c9415bac75baca99a967a2c5c4692d282cbc3eed6f41f81.tscn
assets/scenes/campaign_rooms/components/6eccc67b7db9abe7f9509d025c603c7f0974d44b00600b23c4a38ee1d3e2fbdc.tscn
assets/scenes/campaign_rooms/components/6ecd1d12db8fe1be886baa2e634ae9a455c47f1b0759c83a17e847b9aacb2629.tscn
assets/scenes/campaign_rooms/components/71559af2c546c19211b13ce9e118f7c2e350d7c04dffa601b73ba5a5ac7e813d.tscn
assets/scenes/campaign_rooms/components/71606e7aeeb9f5d594df0d4d9af79f075d0439769ef0894b1b762b2625c23228.tscn
assets/scenes/campaign_rooms/components/7165ae9c501addaff94f9ab43eed3810482d541e205b08d198a5d25f50b011c7.tscn
assets/scenes/campaign_rooms/components/717ed6f7df1bd50436d2ad94de6837cc69cc1acdbd1b3ee24b07588341950fa1.tscn
assets/scenes/campaign_rooms/components/7319bbf6098b5148717f15523d3b049320cb53f82a824f101050ef6d3c5abab8.tscn
assets/scenes/campaign_rooms/components/737e6c5cb650a533dd033f6d7f12b35f17b6e3f8ce511a1765e05014c6bdf0ea.tscn
assets/scenes/campaign_rooms/components/7548dd51df87c67934aeb0f2639e4e356050a6d2af37bfd60447cd139ca3f561.tscn
assets/scenes/campaign_rooms/components/75bc6204a705445328e8afd46faefc4fb40731343442b16e05711b8f6de0a6b4.tscn
assets/scenes/campaign_rooms/components/763a696e48b1d5a141b9532b0eeb91d6dea3459db16f4ca672d11053cef0bd9a.tscn
assets/scenes/campaign_rooms/components/7650a1f2ee9864a71e36a674e74829a2615cad843bc2bb9583679fd13a3dbc62.tscn
assets/scenes/campaign_rooms/components/7716abf10e883a12d6aa4d50010cfa02f8ddb0ec79f14b30c249af9a9bc342bb.tscn
assets/scenes/campaign_rooms/components/7733e49a8bf10eb8f1c223f848ade4a455024feb209b4c23d7e7b1afafaaf879.tscn
assets/scenes/campaign_rooms/components/788365ce585cc01e3905f943427d82bec02fea8cb2cf20487bab5a29bcf48737.tscn
assets/scenes/campaign_rooms/components/799cb9ce03eccb4772539897a6e67008b64824f764be5dc197d675b584c8b79f.tscn
assets/scenes/campaign_rooms/components/7a2e13141d0b0fb5d2ee30d5b35f58616e8a132adf15810c435f009238ef12b3.tscn
assets/scenes/campaign_rooms/components/7ab8fbe41eb11b02e4e51d506aeda0598c28a8ac45a430f88bbb34df2c2fab8b.tscn
assets/scenes/campaign_rooms/components/7ab91c2815235023af0ea92889eb9b53df1b82efe6adb1e12b88e16bd5ad304d.tscn
assets/scenes/campaign_rooms/components/7b72b8bc117ce3c189647e46a525a70bc11daaacb9a3584273e23edc16e47888.tscn
assets/scenes/campaign_rooms/components/7d62644840700e534cc107ea9432267e4bb79b89b2f59cbc7f455fbc4d6aaa50.tscn
assets/scenes/campaign_rooms/components/7dc4f24e86883493554f8555634d1d2bae2af45f02ea56507947f56d2663fea0.tscn
assets/scenes/campaign_rooms/components/803cffddbc045545cb4901bf41d633f30eb16f753fb9689c88cbcc2b76f3266a.tscn
assets/scenes/campaign_rooms/components/8254199d6cc5605e5c88e2fd3367db9a34814c68059180de0de3ff2a83e75a4a.tscn
assets/scenes/campaign_rooms/components/829555f0fe4bd9bec6f3648af33623cd05b90d22daa8dfcbf7b1a12d051d2043.tscn
assets/scenes/campaign_rooms/components/829d4bbb7ca778d6bc63f418a6964da2426318aaa6143ca787225b694befde81.tscn
assets/scenes/campaign_rooms/components/82da21355e35d802190be264387cda2d258a8242406f15093f082d65029b92df.tscn
assets/scenes/campaign_rooms/components/83800d4f40782c74bcff84b3bed9230d42d01d0fd94ed9c0c35efce0e58cb1be.tscn
assets/scenes/campaign_rooms/components/867096901070197603fe8d50601e625c2152a87898eb43ddc73b06ae3ee635c3.tscn
assets/scenes/campaign_rooms/components/87025e0ed7fd194ee5c89ff1ea859dced375314459824972e5a41f52f500dee3.tscn
assets/scenes/campaign_rooms/components/87bf98ce0f7671eda310753155a7d991b75acc34f1d27c82386e25da080752b8.tscn
assets/scenes/campaign_rooms/components/8834f7507d5831e73d2d6b9930ebad3549166aeb430af150850cb7f5bb5b2a4b.tscn
assets/scenes/campaign_rooms/components/8a6e1d56be50d54ec627007808a7283b0bdd7fd44a13b398f76e1d0126df8eb0.tscn
assets/scenes/campaign_rooms/components/8c1d515b8dfe10c24146d427820ee4c6e1628bf1dd07b65279cac995b0542a52.tscn
assets/scenes/campaign_rooms/components/8ceb4ec7d85e7e3379000ef35a12caf8dca035fd26a18ce9eaabb58906f15fbc.tscn
assets/scenes/campaign_rooms/components/8d741fd87093a30062da015bf3bf32edf9479040b1146d6d0e9a5e6b52026cf9.tscn
assets/scenes/campaign_rooms/components/8d97ced62b1fdaa665a6590c9ead9126d0be637985fbe81c8f2b2507a207e6a9.tscn
assets/scenes/campaign_rooms/components/8e4457c4dc02184848125e9c47230bb170d22be298924a53b1ef8ff81487d6f0.tscn
assets/scenes/campaign_rooms/components/909f71ec68ef8952f5cdc507ef8dab62347fe4e72e15418fa6d71e5e1951adf8.tscn
assets/scenes/campaign_rooms/components/914d5f1ceb379acecaa1e8e7f825008a1f15e46ea43be438712acec2f6f97050.tscn
assets/scenes/campaign_rooms/components/919ba408003c63fb90d3d59d7f89013b99d85f93715184b4e687aefd016eff65.tscn
assets/scenes/campaign_rooms/components/91ab25f6e03980f27d76d135438b2ac7435d41ca866b22b61717ea1fe118a56e.tscn
assets/scenes/campaign_rooms/components/93b9f605d40d4bb81091c9e5764f90008b4a552d45fef0c5a9eb21e983b2bfd8.tscn
assets/scenes/campaign_rooms/components/9504cae99e1601fd6cac0979cd50b5bd42996c631d1119418ebd184c88e3fd9a.tscn
assets/scenes/campaign_rooms/components/954322645cbe1766b79ccaa6dc48fbdf8823e3ae1b23ba5dd368a0a4e76180f9.tscn
assets/scenes/campaign_rooms/components/96058696eb4b7c3fa6c9bc86014ac39947c8ff183d01e44214d7dac987b8ff8d.tscn
assets/scenes/campaign_rooms/components/961235dfc16ed073f2efdb152f74421bf87094a7983d8b039911dff14d47057b.tscn
assets/scenes/campaign_rooms/components/97ef26597eb418049bfca4ff13e6db8d9df4794d2c19f2912c07351b4c0aa0e9.tscn
assets/scenes/campaign_rooms/components/97f219c495d5b1fd2ed3194343031009e3e312aa03e859f823d86ecce9d66894.tscn
assets/scenes/campaign_rooms/components/9b3fc0bd6d1b2343bc5d3d0fb33a91c7f8693db55b22256b3e599f2a52bbb3af.tscn
assets/scenes/campaign_rooms/components/9b85d1fb71818248ade6984164d40ca965f05273e2c9ebc49402ad16b7d7a34a.tscn
assets/scenes/campaign_rooms/components/9c0d65b826053211627f1eefd1700fe69390db073508224b264d3775d52eef15.tscn
assets/scenes/campaign_rooms/components/9c7adaab1a065ecb6083f141d90e2a6041c17584288f0f583301b4fef15b0fda.tscn
assets/scenes/campaign_rooms/components/9de84cc74337525bbbd06625a5e04a624f542eb0c9a9806989e3643805bb0d62.tscn
assets/scenes/campaign_rooms/components/a05ac20efb947eb5b92870eb5888b7c3dc334a99155670f2ad54bfebebb28ccf.tscn
assets/scenes/campaign_rooms/components/a093d198bf9ad9848b9ccbcda7bc99d5015cb2fb4574cb8eaa861043056bb717.tscn
assets/scenes/campaign_rooms/components/a129ed89e0ca86fa6359bdd6824c1714b7262653f74a3fadf65b022b3bfc136d.tscn
assets/scenes/campaign_rooms/components/a356c967e40b16f3c05d8a21a60765fd7beb521f671a350481835d4253d28bd3.tscn
assets/scenes/campaign_rooms/components/a39d13a8150f64639e5a08fbbeffb89df52b20fd491809f226b0058447e385bf.tscn
assets/scenes/campaign_rooms/components/a3cc9ced15fe60f0b6db7813c355efaa56d3705ee8707e10d30a06ecfdcd2e06.tscn
assets/scenes/campaign_rooms/components/a65c6756229de5dd64295693fdc6062fa574340190d8570717d7615e4d2654cd.tscn
assets/scenes/campaign_rooms/components/a666fb9ccb0e5364602bf4318755b4caad14508ea5c264da0b67cfd8e6cb7248.tscn
assets/scenes/campaign_rooms/components/a6cc4c3e71513672591860b32046e37af305f0e398bc43c773587101303768cd.tscn
assets/scenes/campaign_rooms/components/a6d38e5ab07e404f4c094d50d14064a1bb7f672310ab330921c3449c487f0014.tscn
assets/scenes/campaign_rooms/components/a762bcf17f7ab6f35bb06a9e2b10deb128f9b8fa2e77bcab7c42c0990bd472a4.tscn
assets/scenes/campaign_rooms/components/a7af94d921c8aa31392dd992f80b25f57db4edc9fadc1ee5e129ae4409a7b0fb.tscn
assets/scenes/campaign_rooms/components/a9165ecdd2024ad1f7c747aac3ee2d5a704e906032ec1ef0c10aabff57ddd1b1.tscn
assets/scenes/campaign_rooms/components/aac665c00b2a2db6408bce7a480aeece8f6a759c58d38de7d26f424f22294da8.tscn
assets/scenes/campaign_rooms/components/ab29cf0e45c9186ed4cc40087fd27578530d5e6444c073aa761e8ca3a784e517.tscn
assets/scenes/campaign_rooms/components/abd3800de0e3630e74ab2e9fc01c0daf5d7f81fa9829290e8603e4b1b353e9c3.tscn
assets/scenes/campaign_rooms/components/ac760f76af97a2cb34db2731166dd167758a7af84acce7326b7b2ff010ccb5ad.tscn
assets/scenes/campaign_rooms/components/ad3ed36eeab47a3a59495afb6df6973aee9f0db424c3e7c76dbf6010382cf3a2.tscn
assets/scenes/campaign_rooms/components/ad6b375cf97618d7d9930cf381b237b7123902335434a61fb592307810407e2f.tscn
assets/scenes/campaign_rooms/components/add5d1f04419452124dd13f851c8f61910247f8f718ee3a8be0f3d67073ea0ec.tscn
assets/scenes/campaign_rooms/components/aeb9f226078151f9e9a9980d2f31d8c5a1d7cd49699c0abd9d6a285aa8736662.tscn
assets/scenes/campaign_rooms/components/b13b5c12e3cd9e285d65a0f1400d3c1e6744580f827e7a26bd03c2d58ea447ac.tscn
assets/scenes/campaign_rooms/components/b16bc2208a88ccf4e59ed0a807fe9f40c7898c9fd4c5f30a5ebfc1e2cc20e176.tscn
assets/scenes/campaign_rooms/components/b3fb997aac529769c56965dd0af0e8a2aed2c8899903aae9e746b249c4abcc29.tscn
assets/scenes/campaign_rooms/components/b5705b7a847cdcca3b3a857ba3b76e27ff3037a57941836887bbd7cc8a242307.tscn
assets/scenes/campaign_rooms/components/b587f24991c7d0564d84edacf9a8e58432da995a4107b2e3f9ce6bf875985fdd.tscn
assets/scenes/campaign_rooms/components/b663c9322593be1ed8fda4f1255b6ae023168c2b57e0dfcaf24d71b48d9095bb.tscn
assets/scenes/campaign_rooms/components/b788b48675b4b8deee44e9089200e87103040da9cbbba5aa895282b00a5de950.tscn
assets/scenes/campaign_rooms/components/b9015341d34205f1c24cfa83161d6a7fa95b2917454ef4548b975b0a30192655.tscn
assets/scenes/campaign_rooms/components/b9db7812a6d7d096d627cfbd34277d7e2195a3aef4d9b99f09aded2d029f907d.tscn
assets/scenes/campaign_rooms/components/ba76cb38e5e900bec142cd0f9db754ac23dc7870261e1909b307039a457341f1.tscn
assets/scenes/campaign_rooms/components/bcb99af6798cc92408c72af3a86a8d0ef1b8958dc615b0ad79ded09d1480dd66.tscn
assets/scenes/campaign_rooms/components/bdb43677a4710ae1e39bd5aa2b8a70ca25c20dc8f8acf6982f039f8eec119f34.tscn
assets/scenes/campaign_rooms/components/be958818c9332a7b9a336b813190782704e72e7f1ff446fb61a3eb646f1a3d0d.tscn
assets/scenes/campaign_rooms/components/bf2ca0293d38855bbc0cc01e86006b2f301ea9c923a10a3e1214989e9e95187b.tscn
assets/scenes/campaign_rooms/components/bf38a28e7623ccd26b96157350f9ebb929e70288318536147a4f89d13de66bf0.tscn
assets/scenes/campaign_rooms/components/c19be7d64586dc5c5892cc662dc7894ed302b7c8ea4a84ae9341fc15b3c8b1c9.tscn
assets/scenes/campaign_rooms/components/c1fae75970f8b7a0d99618b71c2f1e4ee138e4777bbb7fedeb68b6c7c5a75fe6.tscn
assets/scenes/campaign_rooms/components/c3d8b616a4ea763dcf025a5065ba8570c101a697801cbaa5a81573822b7169a7.tscn
assets/scenes/campaign_rooms/components/c50498f5a3cb2c9348098eb660821c40580d6b51ff23e54058f56e70d53267bb.tscn
assets/scenes/campaign_rooms/components/c5a1e2b89851e33a821a9462564154384a96ce908ceb41e3b1476a1297e8d435.tscn
assets/scenes/campaign_rooms/components/c5e481145fa60d1867b8a9dd88ef02af53f2facb3032da94def4f7310fab62af.tscn
assets/scenes/campaign_rooms/components/c6edc3bffa1f10ca44675b208a92c6b13303891f62ef252c13ed62692e2fa89b.tscn
assets/scenes/campaign_rooms/components/c6ef4d1e9bf29af4350bfb34619f75470cb1f769bbd35f463524f960b04bc819.tscn
assets/scenes/campaign_rooms/components/c75c54f58aa8ea93c0c43abaf28d7c93575934ffefe5b59d431780a7e116f1d1.tscn
assets/scenes/campaign_rooms/components/c82757313c7f17b5225ceb6f8d95c42a59b690513a9ba0d1cb962ce2311c80e8.tscn
assets/scenes/campaign_rooms/components/c915c34dedbfc957e6ce0cc549062cb31f32a35d26c1017b92c282a1b26a496f.tscn
assets/scenes/campaign_rooms/components/ca75a716a63d74a5c83cc3cae02968375c9f7ed64a57a4005950530baedb95c3.tscn
assets/scenes/campaign_rooms/components/cb16fd7d74e3b7a0e956cf4dc49495af9dc58e1d90ace9c13450a8ff95b53224.tscn
assets/scenes/campaign_rooms/components/cc85fade66f07e46b64d3ae0153da48ebcb8be56cb671ae689fe2a9e46433e6d.tscn
assets/scenes/campaign_rooms/components/cc9217259ea18add4eab91c1dd74c7eda1cb017c21092f5b5a2c7309a918f611.tscn
assets/scenes/campaign_rooms/components/cd6f5ff6e792f1dda250640d3d6b5f3ff6940d703f76dc9b2545c4e76d864498.tscn
assets/scenes/campaign_rooms/components/d22795e9dd0239a5f5e2755d54bf1826ac129f9c475c1abb92fc1c0f2ad0978c.tscn
assets/scenes/campaign_rooms/components/d2c5c24ede09e7e2df96747c91c10216b09b1049c0b9231d058280058f771b70.tscn
assets/scenes/campaign_rooms/components/d31cd99139fd225e7b7e971628319a0d3692a0534d14b47db22c3708db169baf.tscn
assets/scenes/campaign_rooms/components/d416ec65db4ef6902b49cab5ef74a62e838fb3360aa06cf325f407e07677c2dd.tscn
assets/scenes/campaign_rooms/components/d45f6e62144510d10a19aea42d472a6b97c4f78443d3f288f4bfe7c03fa4357f.tscn
assets/scenes/campaign_rooms/components/d477effad30e8bf4aa29c956b1d80650ec1239630e53825d3ef950b38a05d0b5.tscn
assets/scenes/campaign_rooms/components/d4d9c4ed4550e8d43e619c92a93a0a6436a2cd3e2b1192d51c3cfad1a6092f4d.tscn
assets/scenes/campaign_rooms/components/d4f63d883c96c83a14b7018db016909b34a007e136ae08f1c515ede10587cb75.tscn
assets/scenes/campaign_rooms/components/d4f750bada66661f78769a963da6e3b07da48efbbfd8ab2dd57a6012636ebb35.tscn
assets/scenes/campaign_rooms/components/d668855f1103463f3115bd488b06c5b28087d414d2cfacb84ac375dbc732c923.tscn
assets/scenes/campaign_rooms/components/d67aea555468c70e72f70397266a34b9be6e3fdc4d18432f9e2b41d66000adc7.tscn
assets/scenes/campaign_rooms/components/d90b3e1465a93db63a1de4d60055c5e07e1c26b8ee745bb6d28bdd50b121e208.tscn
assets/scenes/campaign_rooms/components/d949942fdd72ae93debbdfc95a6ea2d28920766d8f59afbf9d3d676cdeab2f98.tscn
assets/scenes/campaign_rooms/components/d9a62894f4b19385246d33539d7ab7f2d1c47d83c8bdb465d1eff9be667d80b4.tscn
assets/scenes/campaign_rooms/components/da035f4b89f50c2d5706a1cde5e48f05fa291b40a40b20ccc9acd1fa6aff9ac0.tscn
assets/scenes/campaign_rooms/components/da71bae6fede0a1a80d838eb33d04d0628d9153e5c38115ebaf0bb4eb5824587.tscn
assets/scenes/campaign_rooms/components/da90c9ec1bf98c8c7e4ca98289e4a791cbfce4ebf2972d813059f13d937f3ccd.tscn
assets/scenes/campaign_rooms/components/db4728f4509cfe3b801b20c16983372d690216ea7730a993909d97452c9a7c49.tscn
assets/scenes/campaign_rooms/components/dbbec75966382126fcd673346a7375af417fcdff2a3ba2656b28a60874009b8c.tscn
assets/scenes/campaign_rooms/components/dda71bb684c0fe6d0105b51d824c3162b95fcbd63bfa7628c58ab4d7b1defa5f.tscn
assets/scenes/campaign_rooms/components/de6d1004e56e99dca4358eb032462ea2a3733f7a7149c6d70ed0bdb5fd4a1090.tscn
assets/scenes/campaign_rooms/components/de6d1a8d9be1e2f57af01fdc118449726a474fdb55066f59c9a44ce259910169.tscn
assets/scenes/campaign_rooms/components/ded77940665fdbaf1ef152c1c0eb2e1b0cfc0b3c9dd688c23e5fc49907eb331e.tscn
assets/scenes/campaign_rooms/components/dfc36bb22152416b187602ac5db73c0b9af26a9118b5ba0cc62efc52afc633fa.tscn
assets/scenes/campaign_rooms/components/dfd2427b7abbd3674bf2a41691e9aad59ab65179319b61bc419ff1f642f3bbee.tscn
assets/scenes/campaign_rooms/components/e05075337ea7c2fae599f1e13eb57bc35e16bf23430b9a0984fe4a5dff807d3b.tscn
assets/scenes/campaign_rooms/components/e0c850a771be6a35eb18da96dc219ce8c51970d800e90d3336e39b6e78c0d6d0.tscn
assets/scenes/campaign_rooms/components/e17c8aa176649c9d105f1697ae061f465abaf4a61576a356d9a1cd24e24e5de8.tscn
assets/scenes/campaign_rooms/components/e1fcb90ebfa922bbd90ea56851debe7dcf9752b3b48df85da67013db1d4ed2c2.tscn
assets/scenes/campaign_rooms/components/e365674058759062a5d45b9c14c6d0308de7202b71ef87fd756a507016bae827.tscn
assets/scenes/campaign_rooms/components/e3839fdd0a49148f1b6c0aa4245321f2bff7dd3386ed8323224797e050eabfb8.tscn
assets/scenes/campaign_rooms/components/e4bcb1969e73da158b236297a1c736c6ebb72c9e8c19a21402740aea69014dac.tscn
assets/scenes/campaign_rooms/components/e4c844d29546e00e6fe479324bf37a8eaf48f1ae349c3f82ae3a09d9de7b6457.tscn
assets/scenes/campaign_rooms/components/e4cc009552d8f12fc02ccbab220274ecf68d4065a5a3bfe2465a5ddf4cd22bb3.tscn
assets/scenes/campaign_rooms/components/e5499e0991996f2161f67ef79384512a2e669666aff7c9990b58b055a657ed90.tscn
assets/scenes/campaign_rooms/components/ead5de6dce83931b3612b83cc6ec40242271ba1726bdebf0f75e35c70b8a10a8.tscn
assets/scenes/campaign_rooms/components/ed31b40864b363eb5bc8f4dddb459a6cdd4a9a7d49e44a9d46eed45202ebabe1.tscn
assets/scenes/campaign_rooms/components/ed3d17b379abe4f40487bb4fca36b57e441a73873cb89364a2abc6f644729d61.tscn
assets/scenes/campaign_rooms/components/ede4bd12b122a41c67d96737d2b782459dd51febad4cfa8b63e2808ba03e7ec0.tscn
assets/scenes/campaign_rooms/components/ee10f46bb3a41a5e898a11b8d6eded425d9bb9cdc24e4119429b372b1eb6b39e.tscn
assets/scenes/campaign_rooms/components/eedadff6610bd17549b8ae25c3413349348e9c5267e9fdbd7795faf81ea49381.tscn
assets/scenes/campaign_rooms/components/f10ff6e92693ce965e8db432b1924c32f62389f88e9d28e656a0ace13a7b1df3.tscn
assets/scenes/campaign_rooms/components/f128e46e98bad6421420855665a34d88c100f79b08a34780b0fb2280e5b55e40.tscn
assets/scenes/campaign_rooms/components/f1ea343a5255174d4f5550dc6f2e05d2441993afffbb4021eaee36181f2ef45c.tscn
assets/scenes/campaign_rooms/components/f23e5090c0d8400a9b48024ec650b3221a0da5f5361b0fb312b1e123aacbf498.tscn
assets/scenes/campaign_rooms/components/f477df907ac26fc414a03be80cf18449abb8975fbf43dc1c16eb149b7d28f044.tscn
assets/scenes/campaign_rooms/components/f4cebe029ff71b7ed9bff865828596c10ce1632bb9919b86a2e71e790852b925.tscn
assets/scenes/campaign_rooms/components/f56a7294b709afee71f43c419c77880d99eabbae84f1853f58cbf63e144bbd7a.tscn
assets/scenes/campaign_rooms/components/f62871ef49dcafe8ef6c250a29cea18599963c34fa414da93da2d38835266156.tscn
assets/scenes/campaign_rooms/components/f76e0ad6373dd1a0e56c28828c3046db2deca97a3747f0c48391f075a9a95090.tscn
assets/scenes/campaign_rooms/components/f7ebbc84005b8d7cb98e5a865074382552e42c62449d2378c74015728e0d130c.tscn
assets/scenes/campaign_rooms/components/f8b50fac13caf0c5b3284e50cc184516e5f760f3c16f2cfab581e77ae97f318f.tscn
assets/scenes/campaign_rooms/components/fa92e3e322d86135b749f1dc2158cf12ccd276bab98abfee36d52e5bb47ff4ee.tscn
assets/scenes/campaign_rooms/components/fdadfe294c08a9341b59ab959dfca3b7a2a9302810a4b32d54b86cae809b6462.tscn
assets/scenes/campaign_rooms/components/fdc928d527e4801199a60603b13034de2c2444360cfa13eddb46d60da811107f.tscn
assets/scenes/campaign_rooms/components/fdeca8c580e598aecff40b387bd05210d588bde71933e9ba1610870157f21b0c.tscn
assets/scenes/campaign_rooms/components/ff3679ffbb8cda4b094519916382924e6fbd19af598cfc5c8dd8697aee9142c9.tscn
assets/ui/HighlightsTheme.tres
assets/ui/OverlayTheme.tres
```


### config


```text
config/coregodot-debug.dv
```


### core


```text
core/autonomy/DuelView.Autonomy.csproj
core/cognition/DuelView.Cognition.csproj
core/dv_lang/DuelView.DvLang.csproj
core/ontology/DuelView.Ontology.csproj
core/reasoning/DuelView.Reasoning.csproj
core/session_cli/DuelView.Cli.csproj
core/simulation/DuelView.Simulation.csproj
core/spatial/DuelView.Spatial.csproj
```


### scenes


```text
scenes/UIpanel/BookUI/BookUI.tscn
scenes/UIpanel/GUI/ActionChoiceGUI.tscn
scenes/UIpanel/GUI/ClockRoomGUI.tscn
scenes/UIpanel/GUI/DeductionGUI.tscn
scenes/UIpanel/GUI/FieldGUI.tscn
scenes/UIpanel/GUI/GuidanceGUI.tscn
scenes/UIpanel/GUI/PersuasionGUI.tscn
scenes/UIpanel/GUI/TaskProgressGUI.tscn
scenes/UIpanel/SaveUI/SaveUI.tscn
scenes/UIpanel/SettingUI/SettingUI.tscn
scenes/UIpanel/TitleUI/TitleUI.tscn
scenes/World/Maps/campus_n.tscn
scenes/World/Maps/campus_ne.tscn
scenes/World/Maps/campus_nw.tscn
scenes/World/Maps/campus_s.tscn
scenes/World/Maps/campus_sw.tscn
scenes/World/Maps/harbor.tscn
scenes/components/architecture/CircularBlastDoor3D.tscn
scenes/components/architecture/DoorFrameClearance.tscn
scenes/components/architecture/ExteriorRoof3D.tscn
scenes/components/architecture/HingedDoor3D.tscn
scenes/components/architecture/HingedWindow3D.tscn
scenes/components/architecture/PropEntity3D.tscn
scenes/components/architecture/RoomContainer3D.tscn
scenes/components/architecture/SmartFloorZone3D.tscn
scenes/components/architecture/SmartRailing3D.tscn
scenes/components/architecture/SmartStair3D.tscn
scenes/components/architecture/SmartWall3D.tscn
scenes/components/characters/ActorActionQueue.tscn
scenes/components/characters/AutonomousActorExecutor.tscn
scenes/components/characters/CampaignRoomHost.tscn
scenes/components/characters/CharacterController3D.tscn
scenes/components/characters/ConversationAnimationLibrary.tscn
scenes/components/characters/ConversationController3D.tscn
scenes/components/characters/ImportedCharacter.tscn
scenes/components/characters/KayKitAnimationLibrary.tscn
scenes/components/characters/PhysicalQueryBudget.tscn
scenes/components/characters/ReachTarget3D.tscn
scenes/components/characters/RoomTransition.tscn
scenes/components/characters/SeatPoseModifier3D.tscn
scenes/components/characters/SeatPostureDriver3D.tscn
scenes/components/characters/SimsBehaviorDirector.tscn
scenes/components/characters/SpatialParticipants3D.tscn
scenes/components/characters/StoryActor.tscn
scenes/components/characters/StoryBody3D.tscn
scenes/components/rendering/AtmosphereRig.tscn
scenes/components/rendering/CameraOcclusion.tscn
scenes/components/rendering/ChamferGeometry.tscn
scenes/components/rendering/CutawayLighting.tscn
scenes/components/rendering/Daylight.tres
scenes/components/rendering/InteriorFinish.tscn
scenes/components/rendering/MaterialLibrary.tscn
scenes/components/rendering/ObserverEnvironment3D.tscn
scenes/components/rendering/PersistentMultiMesh.tscn
scenes/components/rendering/PlayerViewTrace.tscn
scenes/components/rendering/RenderDetailController.tscn
scenes/components/rendering/RpgCameraRig.tscn
scenes/components/rendering/RpgPixelView.tscn
scenes/components/rendering/RpgRenderProfile.tscn
scenes/components/rendering/RpgRenderer.tscn
scenes/components/rendering/SharedPresentationContext3D.tscn
scenes/components/rendering/TopDownCamera3D.tscn
scenes/components/rendering/WorldTimeLighting.tscn
scenes/components/shared/GameAudio.tscn
scenes/components/shared/GameSettings.tscn
scenes/components/shared/HostLifecycle.tscn
scenes/components/shared/LocaleSettings.tscn
scenes/components/shared/MaterialControls.tscn
scenes/components/shared/MaterialTheme.tres
scenes/components/shared/MusicContext.tscn
scenes/components/shared/Sound.tscn
scenes/components/world/BookLeafTurn3D.tscn
scenes/components/world/BookPageStack3D.tscn
scenes/components/world/BookSurfaceDetail3D.tscn
scenes/components/world/DesignCoordinates.tscn
scenes/components/world/PhysicsLayers.tscn
scenes/components/world/StudyDeskStage3D.tscn
scenes/main/Main.tscn
scenes/prefabs/architecture/APArchitecture.tscn
scenes/prefabs/architecture/AP_CompleteShell.tscn
scenes/prefabs/architecture/B2ControlArchitecture.tscn
scenes/prefabs/architecture/B2Control_CompleteShell.tscn
scenes/prefabs/architecture/B2ValveGalleryArchitecture.tscn
scenes/prefabs/architecture/B2ValveGallery_CompleteShell.tscn
scenes/prefabs/architecture/BPArchitecture.tscn
scenes/prefabs/architecture/BP_CompleteShell.tscn
scenes/prefabs/architecture/CPArchitecture.tscn
scenes/prefabs/architecture/CP_CompleteShell.tscn
scenes/prefabs/architecture/ClinicArchitecture.tscn
scenes/prefabs/architecture/Clinic_CompleteShell.tscn
scenes/prefabs/architecture/DPArchitecture.tscn
scenes/prefabs/architecture/DP_CompleteShell.tscn
scenes/prefabs/architecture/LectureArchitecture.tscn
scenes/prefabs/architecture/Lecture_CompleteShell.tscn
scenes/prefabs/architecture/LivingHallArchitecture.tscn
scenes/prefabs/architecture/LivingHall_CompleteShell.tscn
scenes/prefabs/architecture/Main1fF04Architecture.tscn
scenes/prefabs/architecture/Main1fF04_CompleteShell.tscn
scenes/prefabs/architecture/Main1fF05Architecture.tscn
scenes/prefabs/architecture/Main1fF05_CompleteShell.tscn
scenes/prefabs/architecture/Main1fF06Architecture.tscn
scenes/prefabs/architecture/Main1fF06_CompleteShell.tscn
scenes/prefabs/architecture/MainDiningArchitecture.tscn
scenes/prefabs/architecture/MainDining_CompleteShell.tscn
scenes/prefabs/architecture/Mainb1ServiceArchitecture.tscn
scenes/prefabs/architecture/Mainb1Service_CompleteShell.tscn
scenes/prefabs/architecture/Mainb1U01Architecture.tscn
scenes/prefabs/architecture/Mainb1U01_CompleteShell.tscn
scenes/prefabs/architecture/R01Architecture.tscn
scenes/prefabs/architecture/R01_CompleteShell.tscn
scenes/prefabs/architecture/R02Architecture.tscn
scenes/prefabs/architecture/R02_CompleteShell.tscn
scenes/prefabs/architecture/R03Architecture.tscn
scenes/prefabs/architecture/R03_CompleteShell.tscn
scenes/prefabs/architecture/R04Architecture.tscn
scenes/prefabs/architecture/R04_CompleteShell.tscn
scenes/prefabs/architecture/R05Architecture.tscn
scenes/prefabs/architecture/R05_CompleteShell.tscn
scenes/prefabs/architecture/R06Architecture.tscn
scenes/prefabs/architecture/R06_CompleteShell.tscn
scenes/prefabs/architecture/R07Architecture.tscn
scenes/prefabs/architecture/R07_CompleteShell.tscn
scenes/prefabs/architecture/R08Architecture.tscn
scenes/prefabs/architecture/R08_CompleteShell.tscn
scenes/prefabs/architecture/R09Architecture.tscn
scenes/prefabs/architecture/R09_CompleteShell.tscn
scenes/prefabs/architecture/R10Architecture.tscn
scenes/prefabs/architecture/R10_CompleteShell.tscn
scenes/prefabs/architecture/R11Architecture.tscn
scenes/prefabs/architecture/R11_CompleteShell.tscn
scenes/prefabs/architecture/R12Architecture.tscn
scenes/prefabs/architecture/R12_CompleteShell.tscn
scenes/prefabs/architecture/R13Architecture.tscn
scenes/prefabs/architecture/R13_CompleteShell.tscn
scenes/prefabs/architecture/R14Architecture.tscn
scenes/prefabs/architecture/R14_CompleteShell.tscn
scenes/prefabs/architecture/R15Architecture.tscn
scenes/prefabs/architecture/R15_CompleteShell.tscn
scenes/prefabs/architecture/R16Architecture.tscn
scenes/prefabs/architecture/R16_CompleteShell.tscn
scenes/prefabs/architecture/ServiceStairMArchitecture.tscn
scenes/prefabs/architecture/ServiceStairM_CompleteShell.tscn
scenes/prefabs/doors_windows/Door_01.tscn
scenes/prefabs/doors_windows/Door_02.tscn
scenes/prefabs/doors_windows/Door_03.tscn
scenes/prefabs/doors_windows/Door_04.tscn
scenes/prefabs/doors_windows/Door_06.tscn
scenes/prefabs/doors_windows/Door_07.tscn
scenes/prefabs/doors_windows/Door_08.tscn
scenes/prefabs/doors_windows/Door_09.tscn
scenes/prefabs/doors_windows/Door_10.tscn
scenes/prefabs/doors_windows/Door_12.tscn
scenes/prefabs/doors_windows/Door_13.tscn
scenes/prefabs/doors_windows/Door_14.tscn
scenes/prefabs/doors_windows/Door_16.tscn
scenes/prefabs/doors_windows/Door_17.tscn
scenes/prefabs/doors_windows/Door_18.tscn
scenes/prefabs/doors_windows/Door_19.tscn
scenes/prefabs/doors_windows/Door_20.tscn
scenes/prefabs/doors_windows/Door_21.tscn
scenes/prefabs/doors_windows/Door_23.tscn
scenes/prefabs/doors_windows/DoubleArchDoor.tscn
scenes/prefabs/doors_windows/HeavyLock.tscn
scenes/prefabs/doors_windows/HeavySecurityDoor.tscn
scenes/prefabs/doors_windows/HingedWindow_Arch.tscn
scenes/prefabs/doors_windows/InterlockHandle.tscn
scenes/prefabs/doors_windows/LatticeDoor.tscn
scenes/prefabs/doors_windows/PanelDoor.tscn
scenes/prefabs/doors_windows/ThreePartDoorLock.tscn
scenes/prefabs/doors_windows/Window_01.tscn
scenes/prefabs/doors_windows/Window_02.tscn
scenes/prefabs/doors_windows/Window_03.tscn
scenes/prefabs/doors_windows/Window_04.tscn
scenes/prefabs/doors_windows/Window_05.tscn
scenes/prefabs/doors_windows/Window_06.tscn
scenes/prefabs/doors_windows/Window_07.tscn
scenes/prefabs/doors_windows/Window_08.tscn
scenes/prefabs/doors_windows/Window_09.tscn
scenes/prefabs/doors_windows/Window_11.tscn
scenes/prefabs/doors_windows/Window_12.tscn
scenes/prefabs/furniture/Armchair.tscn
scenes/prefabs/furniture/Bed_02.tscn
scenes/prefabs/furniture/BedsideCabinet.tscn
scenes/prefabs/furniture/Bench_01.tscn
scenes/prefabs/furniture/Bench_02.tscn
scenes/prefabs/furniture/Bench_03.tscn
scenes/prefabs/furniture/Bench_04.tscn
scenes/prefabs/furniture/Bookshelf_01.tscn
scenes/prefabs/furniture/Bookshelf_02.tscn
scenes/prefabs/furniture/Cabinet_01.tscn
scenes/prefabs/furniture/Cabinet_02.tscn
scenes/prefabs/furniture/Chair_02.tscn
scenes/prefabs/furniture/Chair_03.tscn
scenes/prefabs/furniture/Chair_04.tscn
scenes/prefabs/furniture/Chair_05.tscn
scenes/prefabs/furniture/Chair_06.tscn
scenes/prefabs/furniture/Chair_07.tscn
scenes/prefabs/furniture/Chair_09.tscn
scenes/prefabs/furniture/CrockeryShelf.tscn
scenes/prefabs/furniture/DormBed.tscn
scenes/prefabs/furniture/Easel.tscn
scenes/prefabs/furniture/FoldingDesk.tscn
scenes/prefabs/furniture/FootSupport.tscn
scenes/prefabs/furniture/Lectern_01.tscn
scenes/prefabs/furniture/Lectern_02.tscn
scenes/prefabs/furniture/LinenCabinet.tscn
scenes/prefabs/furniture/MaintenanceCabinet.tscn
scenes/prefabs/furniture/MedicalBed.tscn
scenes/prefabs/furniture/MedicineCabinet.tscn
scenes/prefabs/furniture/Nightstand.tscn
scenes/prefabs/furniture/PrivacyScreen.tscn
scenes/prefabs/furniture/ServiceCabinet.tscn
scenes/prefabs/furniture/StudyChair.tscn
scenes/prefabs/furniture/StudyDesk.tscn
scenes/prefabs/furniture/Table_01.tscn
scenes/prefabs/furniture/Table_03.tscn
scenes/prefabs/furniture/Table_04.tscn
scenes/prefabs/furniture/Table_05.tscn
scenes/prefabs/furniture/Table_06.tscn
scenes/prefabs/furniture/Table_07.tscn
scenes/prefabs/furniture/Table_09.tscn
scenes/prefabs/furniture/Table_10.tscn
scenes/prefabs/furniture/Table_11.tscn
scenes/prefabs/furniture/VintageWritingDesk.tscn
scenes/prefabs/furniture/Wardrobe.tscn
scenes/prefabs/furniture/WashTable.tscn
scenes/prefabs/interactive_props/Barrel.tscn
scenes/prefabs/interactive_props/Chalkboard_01.tscn
scenes/prefabs/interactive_props/Chalkboard_02.tscn
scenes/prefabs/interactive_props/ControlConsole_01.tscn
scenes/prefabs/interactive_props/ControlConsole_02.tscn
scenes/prefabs/interactive_props/CookerHood.tscn
scenes/prefabs/interactive_props/DishRack_01.tscn
scenes/prefabs/interactive_props/DishRack_02.tscn
scenes/prefabs/interactive_props/Drain.tscn
scenes/prefabs/interactive_props/DryingRack.tscn
scenes/prefabs/interactive_props/GoBoard.tscn
scenes/prefabs/interactive_props/GrainCrates_01.tscn
scenes/prefabs/interactive_props/GrainCrates_02.tscn
scenes/prefabs/interactive_props/GrainPallet.tscn
scenes/prefabs/interactive_props/GrainRack.tscn
scenes/prefabs/interactive_props/GrainSacks.tscn
scenes/prefabs/interactive_props/Handcart.tscn
scenes/prefabs/interactive_props/IndicatorBoard.tscn
scenes/prefabs/interactive_props/IndicatorBoard_02.tscn
scenes/prefabs/interactive_props/IndicatorBoard_03.tscn
scenes/prefabs/interactive_props/IndicatorBoard_04.tscn
scenes/prefabs/interactive_props/KitchenHood.tscn
scenes/prefabs/interactive_props/LongDrain.tscn
scenes/prefabs/interactive_props/MaintenanceCrate.tscn
scenes/prefabs/interactive_props/MusicStand.tscn
scenes/prefabs/interactive_props/OpenBook_01.tscn
scenes/prefabs/interactive_props/OpenBook_02.tscn
scenes/prefabs/interactive_props/PipeRun.tscn
scenes/prefabs/interactive_props/PipeRun_01.tscn
scenes/prefabs/interactive_props/PipeRun_02.tscn
scenes/prefabs/interactive_props/PipeRun_03.tscn
scenes/prefabs/interactive_props/PipeRun_04.tscn
scenes/prefabs/interactive_props/PipeRun_05.tscn
scenes/prefabs/interactive_props/PipeRun_06.tscn
scenes/prefabs/interactive_props/PipeRun_07.tscn
scenes/prefabs/interactive_props/PipeRun_09.tscn
scenes/prefabs/interactive_props/PipeRun_10.tscn
scenes/prefabs/interactive_props/PipeRun_11.tscn
scenes/prefabs/interactive_props/PipeRun_12.tscn
scenes/prefabs/interactive_props/PipingSchematic.tscn
scenes/prefabs/interactive_props/PlantingCounter.tscn
scenes/prefabs/interactive_props/PlatformScale.tscn
scenes/prefabs/interactive_props/ReactionVessel.tscn
scenes/prefabs/interactive_props/RecordStation_01.tscn
scenes/prefabs/interactive_props/RecordStation_02.tscn
scenes/prefabs/interactive_props/RecordStation_03.tscn
scenes/prefabs/interactive_props/RecordsPillar.tscn
scenes/prefabs/interactive_props/Schematic.tscn
scenes/prefabs/interactive_props/ServiceChest.tscn
scenes/prefabs/interactive_props/Sink.tscn
scenes/prefabs/interactive_props/Sink_01.tscn
scenes/prefabs/interactive_props/Sink_03.tscn
scenes/prefabs/interactive_props/Sink_04.tscn
scenes/prefabs/interactive_props/Stove.tscn
scenes/prefabs/interactive_props/Stove_02.tscn
scenes/prefabs/interactive_props/ValveStation_01.tscn
scenes/prefabs/interactive_props/ValveStation_02.tscn
scenes/prefabs/interactive_props/VentGrille_01.tscn
scenes/prefabs/interactive_props/VentGrille_02.tscn
scenes/prefabs/interactive_props/WoodenChest.tscn
scenes/prefabs/interactive_props/WorkCounter_01.tscn
scenes/prefabs/interactive_props/WorkCounter_02.tscn
scenes/prefabs/lighting_decor/BankerLamp.tscn
scenes/prefabs/lighting_decor/BankerLamp_02.tscn
scenes/prefabs/lighting_decor/BankerLamp_03.tscn
scenes/prefabs/lighting_decor/BankerLamp_04.tscn
scenes/prefabs/lighting_decor/BottleRack.tscn
scenes/prefabs/lighting_decor/Bottles_01.tscn
scenes/prefabs/lighting_decor/Bottles_02.tscn
scenes/prefabs/lighting_decor/CagedLamp.tscn
scenes/prefabs/lighting_decor/CagedLamp_01.tscn
scenes/prefabs/lighting_decor/CagedLamp_02.tscn
scenes/prefabs/lighting_decor/CagedLamp_03.tscn
scenes/prefabs/lighting_decor/CagedLamp_05.tscn
scenes/prefabs/lighting_decor/CagedLamp_06.tscn
scenes/prefabs/lighting_decor/CeramicPitcher.tscn
scenes/prefabs/lighting_decor/Chandelier_01.tscn
scenes/prefabs/lighting_decor/Chandelier_02.tscn
scenes/prefabs/lighting_decor/ClothRack.tscn
scenes/prefabs/lighting_decor/CookingPot.tscn
scenes/prefabs/lighting_decor/CookingPot_02.tscn
scenes/prefabs/lighting_decor/CookingPot_03.tscn
scenes/prefabs/lighting_decor/Cup.tscn
scenes/prefabs/lighting_decor/Painting_01.tscn
scenes/prefabs/lighting_decor/Painting_02.tscn
scenes/prefabs/lighting_decor/Painting_03.tscn
scenes/prefabs/lighting_decor/Painting_04.tscn
scenes/prefabs/lighting_decor/Painting_05.tscn
scenes/prefabs/lighting_decor/Painting_06.tscn
scenes/prefabs/lighting_decor/Painting_07.tscn
scenes/prefabs/lighting_decor/Painting_08.tscn
scenes/prefabs/lighting_decor/Painting_09.tscn
scenes/prefabs/lighting_decor/Painting_10.tscn
scenes/prefabs/lighting_decor/Painting_11.tscn
scenes/prefabs/lighting_decor/Painting_12.tscn
scenes/prefabs/lighting_decor/Painting_13.tscn
scenes/prefabs/lighting_decor/Painting_14.tscn
scenes/prefabs/lighting_decor/Painting_15.tscn
scenes/prefabs/lighting_decor/Painting_16.tscn
scenes/prefabs/lighting_decor/Painting_17.tscn
scenes/prefabs/lighting_decor/PendantLamp.tscn
scenes/prefabs/lighting_decor/Planter_01.tscn
scenes/prefabs/lighting_decor/Planter_02.tscn
scenes/prefabs/lighting_decor/Pot.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_01.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_02.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_03.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_04.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_05.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_06.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_07.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_08.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_09.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_10.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_11.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_12.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_13.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_14.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_15.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_16.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_17.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_18.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_19.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_20.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_21.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_23.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_24.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_25.tscn
scenes/prefabs/lighting_decor/ResidentialAccessories_26.tscn
scenes/prefabs/lighting_decor/RoomRug.tscn
scenes/prefabs/lighting_decor/Rug_01.tscn
scenes/prefabs/lighting_decor/SeedlingPot.tscn
scenes/prefabs/lighting_decor/TablePlate.tscn
scenes/prefabs/lighting_decor/WallLantern.tscn
scenes/prefabs/lighting_decor/WallLantern_02.tscn
scenes/prefabs/lighting_decor/WallLantern_03.tscn
scenes/prefabs/lighting_decor/WallLantern_04.tscn
scenes/rooms/A-P.tscn
scenes/rooms/B-102.tscn
scenes/rooms/B-P.tscn
scenes/rooms/C-P.tscn
scenes/rooms/D-P.tscn
scenes/rooms/F-104.tscn
scenes/rooms/R01.tscn
scenes/rooms/R02.tscn
scenes/rooms/R03.tscn
scenes/rooms/R04.tscn
scenes/rooms/R05.tscn
scenes/rooms/R06.tscn
scenes/rooms/R07.tscn
scenes/rooms/R08.tscn
scenes/rooms/R09.tscn
scenes/rooms/R10.tscn
scenes/rooms/R11.tscn
scenes/rooms/R12.tscn
scenes/rooms/R13.tscn
scenes/rooms/R14.tscn
scenes/rooms/R15.tscn
scenes/rooms/R16.tscn
scenes/rooms/S-102.tscn
scenes/rooms/W-102.tscn
scenes/rooms/b2_control.tscn
scenes/rooms/b2_valve_gallery.tscn
scenes/rooms/clinic.tscn
scenes/rooms/lecture.tscn
scenes/rooms/living_hall.tscn
scenes/rooms/main1f_f04.tscn
scenes/rooms/main1f_f05.tscn
scenes/rooms/main1f_f06.tscn
scenes/rooms/main_dining.tscn
scenes/rooms/mainb1_service.tscn
scenes/rooms/mainb1_u01.tscn
scenes/rooms/service_stair_M.tscn
```


### tools


```text
tools/godot_architect/LiveBuilderOverlay.tscn
tools/godot_architect/PrefabPaletteDock.tscn
tools/sandbox/AuthoringSandbox.tscn
tools/sandbox/Day1Harness.tscn
tools/verification/MovementRegression.tscn
tools/web_building_editor/rooms_yaml/A-P.yaml
tools/web_building_editor/rooms_yaml/B-102.yaml
tools/web_building_editor/rooms_yaml/B-P.yaml
tools/web_building_editor/rooms_yaml/C-P.yaml
tools/web_building_editor/rooms_yaml/D-P.yaml
tools/web_building_editor/rooms_yaml/F-104.yaml
tools/web_building_editor/rooms_yaml/F01.yaml
tools/web_building_editor/rooms_yaml/R01.yaml
tools/web_building_editor/rooms_yaml/R02.yaml
tools/web_building_editor/rooms_yaml/R03.yaml
tools/web_building_editor/rooms_yaml/R04.yaml
tools/web_building_editor/rooms_yaml/R05.yaml
tools/web_building_editor/rooms_yaml/R06.yaml
tools/web_building_editor/rooms_yaml/R07.yaml
tools/web_building_editor/rooms_yaml/R08.yaml
tools/web_building_editor/rooms_yaml/R09.yaml
tools/web_building_editor/rooms_yaml/R10.yaml
tools/web_building_editor/rooms_yaml/R11.yaml
tools/web_building_editor/rooms_yaml/R12.yaml
tools/web_building_editor/rooms_yaml/R13.yaml
tools/web_building_editor/rooms_yaml/R14.yaml
tools/web_building_editor/rooms_yaml/R15.yaml
tools/web_building_editor/rooms_yaml/R16.yaml
tools/web_building_editor/rooms_yaml/S-102.yaml
tools/web_building_editor/rooms_yaml/W-102.yaml
tools/web_building_editor/rooms_yaml/all_rooms.yaml
tools/web_building_editor/rooms_yaml/b2_control.yaml
tools/web_building_editor/rooms_yaml/b2_valve_gallery.yaml
tools/web_building_editor/rooms_yaml/clinic.yaml
tools/web_building_editor/rooms_yaml/lecture.yaml
tools/web_building_editor/rooms_yaml/living_hall.yaml
tools/web_building_editor/rooms_yaml/main1f_f04.yaml
tools/web_building_editor/rooms_yaml/main1f_f05.yaml
tools/web_building_editor/rooms_yaml/main1f_f06.yaml
tools/web_building_editor/rooms_yaml/main_dining.yaml
tools/web_building_editor/rooms_yaml/mainb1_service.yaml
tools/web_building_editor/rooms_yaml/mainb1_u01.yaml
tools/web_building_editor/rooms_yaml/service_stair_M.yaml
```


## 16. 设计与资料索引


### 仓库内相关文档


- `docs/01 世界观、人物卡与博弈设定.md`

- `docs/02 地理箱庭、文献与六章剧情.md`

- `docs/03 美术设定、界面与音乐声学.md`

- `docs/12 房间布局.md`

- `docs/22 证据链与六章推理设计.md`

- `docs/23 角色行为策略与认知边界.md`

- `docs/31 系统架构与核心机制.md`

- `docs/32 技术路线.md`

- `docs/33 实施与验收.md`

- `docs/34 UI与场景系统拆分设计.md`

- `docs/35 Core生活模拟基础与特色能力拆分.md`

- `docs/40_MOVEMENT_HOTFIX.md`

- `docs/REFACTOR_HANDOFF.md`

- `docs/five-day-play.md`

- `docs/five-day-runtime-mechanics.md`


### 外部参考


- [EA：Ways to Start Playing The Sims 4](https://www.ea.com/games/the-sims/the-sims-4/how-to-play-the-sims)：确认生活需要、关系、成长与生活目标的玩法范围，不代表 EA 内部源码架构。

- [Ian Horswill：Needs-Based AI](https://ianhorswill.github.io/DPGD/Needs-based_AI.html)：参考需求效用与物件交互广告的通用技术模式，不声称复刻某代模拟人生调度实现。

- [EA Help：The Sims 4 Skills](https://help.ea.com/en/articles/the-sims/the-sims-4/skills/)：技能由活动成长、等级解锁；用于 B10 类型职责参照。

- [EA Help：The Sims 4 Careers](https://help.ea.com/en/articles/the-sims/the-sims-4/the-sims-4-careers/)：职业、日程、晋升与奖励是生活成长维度，不要求当前项目立即实现全部。

- [Control：GUI 输入、焦点与主题](https://docs.godotengine.org/en/stable/classes/class_control.html)：引擎提供 GUI 布局与事件原语；焦点、mouse_filter 和 accept_event 仍需项目配置。

- [Pausing games and process mode](https://docs.godotengine.org/en/stable/tutorials/scripting/pausing_games.html)：SceneTree.paused 和节点 process_mode 管理暂停行为；它们不自动管理外部 C# 仿真调度。

- [SubViewport](https://docs.godotengine.org/en/stable/classes/class_subviewport.html)：可把 UI 输出为纹理用于 3D；独立 SubViewport 默认不接收输入事件。

- [Audio buses](https://docs.godotengine.org/en/stable/tutorials/audio/audio_buses.html)：内置总线路由、效果与音量处理；事件选曲、并发策略和暂停策略是项目职责。

- [AudioStreamPlayer3D](https://docs.godotengine.org/en/stable/classes/class_audiostreamplayer3d.html)：内置空间衰减、方向性与监听器；隐藏节点不会自动停止声音，也不等于实现隔墙声学传播。

- [ArrayMesh](https://docs.godotengine.org/en/stable/classes/class_arraymesh.html)：数组生成网格是内置能力；墙体拓扑、洞口、尺寸、碰撞和语义需项目生成。

- [PackedScene](https://docs.godotengine.org/en/stable/classes/class_packedscene.html)：内置场景实例化和打包；序列化的子节点受 owner 影响，不能只 add_child 就假定完整保存。

- [EditorPlugin](https://docs.godotengine.org/en/stable/classes/class_editorplugin.html)：用于扩展编辑器功能；不作为导出游戏中运行时建造的依赖。

- [EditorUndoRedoManager](https://docs.godotengine.org/en/stable/classes/class_editorundoredomanager.html)：编辑器插件使用编辑器历史管理；非编辑器用途使用 UndoRedo。

- [CharacterBody3D](https://docs.godotengine.org/en/stable/classes/class_characterbody3d.html)：提供脚本驱动角色的碰撞与滑动接口；运动意图、速度和状态机由项目负责。

- [Using NavigationAgents](https://docs.godotengine.org/en/stable/tutorials/navigation/navigation_using_navigationagents.html)：寻路/跟随计算不会自动移动父节点；NavigationAgent 是可选助手，不要求替换现有 NavigationServer 路径代码。

- [Using AnimationTree](https://docs.godotengine.org/en/stable/tutorials/animation/animation_tree.html)：内置动画混合与状态切换；root motion 仍需显式交给角色运动代码消费，不能形成第二个位置主权。

- [Running code in the editor](https://docs.godotengine.org/en/stable/tutorials/plugins/running_code_in_the_editor.html)：@tool 使代码在编辑器运行；必须区分编辑器和游戏语义，EditorScript 不自动提供撤销历史。

- [Scene organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html)：按关系和职责组织场景、限制耦合；不按视觉空间位置机械划所有代码。


### 本文件覆盖限制


- 结构、现有映射、目标边界和验收契约可在本文件内阅读；没有完整业务源码正文和实际运行回执。

- 不提供虚假的游戏测试结论。能力开关、生活内容裁剪和运行解耦仍是待实施设计。

- 代码/资源索引依据当前普通文件；不追踪符号链接的外部运行时，不包含缓存与生成产物。

- 设计系统、当前程序集和文件归属是三个视角，不要机械一一对应。
