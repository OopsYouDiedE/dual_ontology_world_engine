# 双本体世界引擎（Dual-Ontology World Engine）重构与迁移开发手册

> **文档定位**：本文件为仓库唯一工程重构执行规范（Engineering Master Spec）。所有人类开发者与 AI 编码代理（Claude Code / SubAgents）在执行代码清理、模块迁移、3D 场景建造改造、UI 合并与可视化工具开发时，必须严格遵守本文件限定的目录结构、文件职责映射与依赖边界。任何未在本规范目录树中注册的路径或违反依赖矩阵的引用，均将被 CI 门禁直接拦截。

---

## 一、强制锁定的终态代码库目录结构（Target Directory Tree）

重构完成后，全仓严格限定为以下 **7 个顶层功能目录**。彻底废除离线 Python 整房烘焙与外部裸模型拼接，转向 **Godot 原生参数化建筑建造 + 预配置智能预制件（Smart Prefabs）** 架构。

```text
dual_ontology_world_engine/
├── project.godot                     # Godot 4.7 .NET 主工程配置（启用原生 3D 建筑师插件）
├── DuelView.sln                      # 唯一 .NET 解决方案入口
├── MECHANISM_MAP.yaml                # AI 与人类共享的机制瞬时定位索引表（强制与代码同步）
├── config/
│   └── coregodot-debug.dv            # 默认启动配置（固定指向 res://assets/data/scenarios/campaign.dv）
│
├── core/                             # [C# 确定性逻辑核心层] 8 大内聚子模块，严禁引用 Godot 命名空间
│   ├── dv_lang/                      # 01. DV 词法/语法解析与 AOT 编译器 (DuelView.DvLang.csproj)
│   ├── ontology/                     # 02. 双本体状态树、引用隔离与快照 (DuelView.Ontology.csproj)
│   ├── cognition/                    # 03. 局部感知投影、贝叶斯聚合与源质侵蚀 (DuelView.Cognition.csproj)
│   ├── reasoning/                    # 04. 带符号霍恩逻辑、自然演绎与证明核验 (DuelView.Reasoning.csproj)
│   ├── autonomy/                     # 05. 凹性动机求解器与 Sim4 任务规划 (DuelView.Autonomy.csproj)
│   ├── spatial/                      # 06. 导航图、门洞视锥策略与房间生命周期 (DuelView.Spatial.csproj)
│   ├── simulation/                   # 07. 双轨因果时钟、单步流水线与战役装配 (DuelView.Simulation.csproj)
│   └── session_cli/                  # 08. 二阶段会话门面、差量投影与 CLI 宿主 (DuelView.Cli.csproj)
│
├── scenes/                           # [Godot 4.7 呈现、预制件与原生 3D 建造层]
│   ├── main/                         # 唯一启动入口与 C# 宿主桥接网关
│   │   ├── Main.tscn & Main.gd       # 游戏根场景与生命周期总协调器
│   │   ├── SessionBridge.cs          # 合并原 CharacterSessionBridge 与 GameEngine 的唯一 C# 会话桥
│   │   ├── DvPresentationContent.cs  # DV 表现层声明转 Godot Dictionary 缓存桥
│   │   ├── GodotHostValues.cs        # Variant 与 .NET 对象零 GC 互转工具
│   │   └── CasebookPersistence.cs    # 案卷与手写笔记序列化扩展
│   │
│   ├── UIpanel/                      # [简化后的 12 对核心 UI 体系]
│   │   ├── BookShell/                # 书本外壳：支持 3D 弯曲纸面反投影与 F2 2D 平面直通双模式
│   │   ├── Spreads/                  # 4 套数据驱动的双页书本标准模板（替代原 B01~B08 8 套独立书页）
│   │   ├── Field/                    # 书外现场交互 UI（FieldHUD, InteractionMenu, DialogueUI 等）
│   │   └── Shared/                   # 全局模态框与输入路由（ModalAlertUI, UIInput）
│   │
│   ├── prefabs/                      # [已配置好动作/交互/碰撞的 Godot 智能预制件库 (Smart Prefabs)]
│   │   ├── doors_windows/            # 带开关动画、门洞框线与音效的门窗预制件 (.tscn)
│   │   ├── furniture/                # 带坐姿调整器 (SeatPoseModifier3D) 与站位锚点的家具预制件 (.tscn)
│   │   ├── interactive_props/        # 带调查动画、证据槽位标签与状态切换的机关/证物预制件 (.tscn)
│   │   └── lighting_decor/           # 带光源参数、遮挡剔除组与手绘着色器的灯具/装饰预制件 (.tscn)
│   │
│   ├── rooms/                        # [人类可直接在 Godot 中点击拖拽编辑的 36 个正式房间场景 (.tscn)]
│   │
│   └── components/                   # [3D 核心机制与参数化建筑组件库]
│       ├── architecture/             # @tool 点击拖拽式结构组件 (SmartWall3D, SmartRailing3D, SmartStair3D 等)
│       ├── characters/               # 角色控制、视线检测、对话演出与 CampaignRoomHost.gd
│       ├── rendering/                # 手绘低模渲染、摄像机、光照与剖切遮挡控制
│       ├── shared/                   # Autoload 单例（GameAudio, GameSettings, LocaleSettings）与宿主组件
│       └── world/                    # 3D 书桌舞台、地图视图与现场道具交互执行器
│
├── assets/                           # [纯原始静态媒体资产层] 严禁存放任何 .gd、.cs 或未配置的拼接场景！
│   ├── data/
│   │   ├── scenarios/                # campaign.dv 及分日剧本、角色作息、规则声明
│   │   └── presentation/             # campaign_rooms.dv（由 Godot 房间保存时自动生成同步）
│   ├── meshes/                       # 供 scenes/prefabs/ 引用的底层原子级 .glb 网格模型（家具/门扇/摆件）
│   ├── characters/                   # 3.5 头身素体、17 个角色 GLB、立绘切图与语音文件
│   ├── audio/                        # LUFS 标准化无缝循环配乐 (.ogg) 与音效
│   └── shaders/                      # 四象限手绘材质、书页弯曲积分、二阶拉普拉斯勾线与 Bayer 抖动着色器
│
├── tools/                            # [人类可视化工作台与 Godot 原生建造插件]
│   ├── godot_architect/              # Godot 原生 3D 房间建造器插件 (@tool 编辑器面板 + 游戏内 F4 实时建造台)
│   ├── studio_web/                   # 剧情与时间线工作台（五日作息甘特图 + 案件证据霍恩逻辑 DAG 画布）
│   ├── sandbox/                      # Godot 内嵌可视化调试沙盒 (AuthoringSandbox.tscn, DebugOverlay3D.gd)
│   ├── story/                        # 剧本静态证明核验工具 (author.py, ScenarioAuthoring.cs)
│   └── ci/                           # 仓库卫生与架构防腐门禁 (verify_repo_hygiene.py)
│
└── mcp/                              # [AI 自动化测试、角色/音频资产制备与编译服务层]
    ├── game/                         # MCP JSON-RPC 服务、TCP 控制器 (McpControl.gd) 与看门狗
    ├── asset_pipeline/               # 角色骨骼校验与音轨合成母带管线
    ├── build/                        # DV 编译缓存管理 (dv_toolchain.py)
    └── verification/                 # 自动化回归测试套件与黄金基准校验
```

---

## 二、模块依赖矩阵与边界契约（Dependency Architecture）

### 1. C# 核心程序集有向无环依赖图（Assembly DAG）
`core/` 下的代码按职责划分为 4 个编译层级，**下层绝对禁止反向引用上层，且整个 `core/` 目录严禁引用 `GodotSharp.dll`**：

$$
\text{Level 0: } \mathtt{DuelView.DvLang} \longrightarrow \text{Level 1: } \mathtt{DuelView.Ontology} \longrightarrow \text{Level 2: } \begin{cases} \mathtt{DuelView.Cognition} \\ \mathtt{DuelView.Reasoning} \\ \mathtt{DuelView.Spatial} \end{cases} \longrightarrow \text{Level 3: } \begin{cases} \mathtt{DuelView.Autonomy} \\ \mathtt{DuelView.Simulation} \end{cases} \longrightarrow \text{Level 4: } \mathtt{DuelView.SessionCli}
$$

| 层级 | 程序集 / 目录 | 允许引用的内部依赖 | 核心边界契约 |
| :--- | :--- | :--- | :--- |
| **L0** | `core/dv_lang/` | 无（零内部依赖） | 仅负责将 `.dv` 文本解析为不可变 AST 与 Schema 校验，不感知运行时状态 |
| **L1** | `core/ontology/` | `dv_lang` | 定义物理客观域（`domain="physical"`）与主观心智域（`domain="mind"`）数据结构及 `ReadCell` 越界拦截 |
| **L2** | `core/cognition/` | `dv_lang`, `ontology` | 实现 `F_dv_bind`、`F_dv_perceive`、贝叶斯冲突聚合与源质侵蚀记忆截断 |
| **L2** | `core/reasoning/` | `dv_lang`, `ontology` | 实现带符号霍恩逻辑（`CaseTheory`）、自然演绎与极小可信核心证明器（`CaseProofChecker`） |
| **L2** | `core/spatial/` | `dv_lang`, `ontology` | 维护房间拓扑图、门洞视锥可通达策略（`PortalPolicy`）与槽位占用锁 |
| **L3** | `core/autonomy/` | `ontology`, `cognition`, `reasoning`, `spatial` | 基于凹性满意度公式 $Satisfaction(v) = v - \frac{v^2}{200}$ 计算动机效用与 NPC 任务规划 |
| **L3** | `core/simulation/` | L0 $\sim$ L2 全部 + `autonomy` | 驱动双轨流速时钟（`SimulationClock`）、单步切片序列（`RuntimeSession.Step`）与五日战役拼装 |
| **L4** | `core/session_cli/` | L0 $\sim$ L3 全部 | 封装 `Prepare` $\to$ `Adopt` $\to$ `Complete` 二阶段派发、`HostStateDelta` 差量投影与 `NativeCli` |
| **L5** | `scenes/main/*.cs` | `core/session_cli` + `GodotSharp` | **唯一允许同时引用 `Godot` 与 `DuelView.*` 的桥接层**，负责将 C# 差量转换为 Godot Variant |

### 2. Godot Autoload 全局单例依赖顺序
在 `project.godot` 中严格固定以下 4 个 Autoload 单例的初始化顺序，禁止新增全局单例：
1. `GameLocale` (`res://scenes/components/shared/LocaleSettings.gd`)：多语言字典加载，无外部依赖。
2. `GameAudio` (`res://scenes/components/shared/GameAudio.tscn`)：双轨 BGM 交叉淡化与 8 通道音效池。
3. `GameSettings` (`res://scenes/components/shared/GameSettings.gd`)：依赖 `GameLocale` 与 `GameAudio`，管理窗口、画质、回放与键位配置。
4. `GameMcp` (`res://mcp/game/McpControl.gd`)：自动化测试 TCP 监听挂载点，置于最后以确保可读取全部就绪节点。

### 3. GDScript $\leftrightarrow$ C# 跨语言调用契约
* **单点通信法则**：GDScript 业务组件（UI 面板、3D 角色控制器、智能预制件）**严禁**直接实例化任何 C# 逻辑类。所有对引擎状态的查询与动作派发，必须通过 `Main.gd` 持有的 `SessionBridge.cs`（实时会话）和 `DvPresentationContent.cs`（静态表现配置）两个网关节点完成。
* **零 GC 数据交换**：`SessionBridge.cs` 向 GDScript 返回状态更新时，必须通过 `GodotHostValues.cs` 复用缓存键并仅传递增量字典（`HostStateDelta`），禁止每帧全量序列化完整世界状态。

---

## 三、3D 场景原生可视化建造与智能预制件规范（Native 3D Scene Builder Spec）

为彻底解决“在 Blender 写 Python 脚本生成房间、或在外部网页编辑器里摆放未配置动作的裸模型”的痛点，建立 **Godot 原生两级 3D 场景建造体系**：

```text
┌────────────────────────────────────────────────────────────────────────────┐
│ 第一级：智能预制件工坊 (scenes/prefabs/*.tscn) — 一次配置，到处拖拽复用      │
│  底层 .glb 模型 ──> 挂载 PropEntity3D / HingedDoor3D                        │
│                 ├── 绑定 AnimationPlayer (开门/抽屉/机关动画与音效触发帧)   │
│                 ├── 放置 ReachTarget3D / SeatPoseModifier3D (角色动作锚点) │
│                 ├── 配置 CollisionShape3D 与四象限手绘着色器参数            │
│                 └── 预设默认交互标签 (interact_type, slot_capacity)         │
├────────────────────────────────────────────────────────────────────────────┤
│ 第二级：Godot 原生房间建造器 (tools/godot_architect/) — 点击拖拽实时拼装    │
│  • 墙壁画笔 (SmartWall3D)   : 鼠标点击起点 -> 拖拽终点 -> 放入门窗自动开洞  │
│  • 栏杆画笔 (SmartRailing3D): 点击折线/曲线路径点 -> 自动阵列立柱与扶手     │
│  • 楼梯画笔 (SmartStair3D)  : 拖拽起点与层高控制柄 -> 自动生成踏步与侧栏杆  │
│  • 预制件面板 (Prefab Dock) : 直接拖拽第一级已配好动作的 .tscn 放入房间，   │
│                               实时校验 3.5 头身角色站位空间 (SeatingFit.cs) │
└────────────────────────────────────────────────────────────────────────────┘
```

### 1. 点击拖拽式参数化结构组件（`scenes/components/architecture/`）
全部标记为 `@tool` 脚本，配合 `tools/godot_architect/ArchitectGizmoPlugin.gd` 在 Godot 3D 视口（以及游戏内按 `F4` 呼出的实时建造模式）中通过鼠标点击与控制柄拖拽完成建模：

| 结构组件名称 | 合并/升级的原脚本 | 人类点击拖拽操作方式与自动化机制 |
| :--- | :--- | :--- |
| **`SmartWall3D.gd`** | `WallSurface3D`, `SkirtingBoard3D`, `CutawayCap3D`, `WallOcclusionController`, `OpeningFrame3D` | **点击起点 $\to$ 拖拽终点**：沿 `RoomGrid`（默认 $0.25\text{m}$ 步进）拉伸生成墙体、底部踢脚线与顶部剖切封口（驱动 `CutawayGeometry.cs`）。**门窗自动打洞**：当把 `doors_windows/` 下的门窗预制件拖到墙面上时，自动根据门窗包围盒将墙体拆分为左段、右段与门楣过梁，并生成 `OpeningFrame3D` 门框，无需任何 Blender 布尔运算！ |
| **`SmartRailing3D.gd`** | `CurveRailing3D`, `RailingAssemblyRenderer`, `RailingHandrail3D` | **路径点点击绘制**：继承/包装 `Path3D`。人类在地板边缘或二楼挑空处依次点击拐角锚点（支持直线折线与贝塞尔圆弧），脚本实时沿曲线等距阵列栏杆立柱（Balusters）、转角大立柱（Newel Posts）、连续扶手网格（Handrail）与碰撞体。 |
| **`SmartStair3D.gd`** | `StraightStair3D`, `ChamferGeometry` | **双控制柄拖拽**：拖拽地面起点柄与上层平台终点柄，根据标准步高（$0.18\text{m}$）自动计算踏步数量、生成倒角踏步网格、斜坡碰撞体、导航连接线，并在勾选 `has_left_railing` / `has_right_railing` 时自动派生两侧联动 `SmartRailing3D`。 |
| **`SmartFloorZone3D.gd`** | `FloorSurface3D`, `CeilingSurface3D`, `RoomGrid`, `RoomArtFinish` | **多边形/矩形框选**：点击拖拽拉出房间地面多边形，自动生成地板拼花网格、天花板、网格辅助线，并向 `RoomStreamingController.cs` 注册房间视锥剔除包围盒。 |

### 2. 已配置好动作与特性的“智能预制件”标准（`scenes/prefabs/*.tscn`）
**铁律：房间场景（`scenes/rooms/*.tscn`）中严禁直接拖入未配置的 `assets/meshes/*.glb` 裸模型！** 所有场景物体必须先在 `scenes/prefabs/` 中封装为标准化的智能预制件（Smart Prefab），其固定节点树结构如下：

```text
Prop_StudyDesk.tscn (Node3D, 挂载 PropEntity3D.gd 或专属交互子类)
 ├── VisualRoot (Node3D)
 │    └── MeshInstance3D (引用 assets/meshes/study_desk.glb，已配置 painterly_surface.gdshader 四象限材质)
 ├── AnimationPlayer (已配置好 "open_drawer", "inspect_highlight", "shake" 等原生动画与音效帧)
 ├── StaticBody3D / Area3D
 │    └── CollisionShape3D (已匹配好的物理阻挡与鼠标射线拾取盒)
 ├── InteractionSlots (Node3D)
 │    ├── ReachTarget_Front (ReachTarget3D.gd — 已调好 3.5 头身角色站立位置、朝向与手部触达点)
 │    └── SeatAnchor_Chair (SeatPoseModifier3D.gd — 若为座椅，已配好角色坐姿 IK 骨骼偏移)
 └── EvidenceMarkers (Node3D)
      └── ClueSpot_Drawer (Marker3D — 证据生成点与特写摄像机机位锚点)
```

**智能预制件在房间建造时的核心优势**：
1. **即放即用（Zero-Setup Placement）**：从 `Prefab Dock` 拖入房间时，该物体已经自带开门/抽屉动画、音效、碰撞体、手绘材质和角色交互站位点。
2. **实时人机工学碰撞预览（Live Ergonomic Footprint）**：拖拽移动预制件时，视口实时渲染其内置的 `ReachTarget3D` 小人脚印。当脚印与 `SmartWall3D` 或其他家具发生干涉时，通过 `SeatingFit.cs` 实时将脚印变红告警，彻底杜绝“摆了椅子但游戏里小人坐不上去或穿模”的问题。
3. **检查器一键绑定 `.dv` 逻辑 ID**：选中房间内的任意预制件实例，在右侧 Inspector 面板的 `dv_slot_id` 下拉框中直接选择 `campaign.dv` 里定义的逻辑槽位（如 `slot:dean_desk_drawer`），点击测试按钮即可当场播放预制件动作并预览角色走过去交互的全过程。

### 3. 房间保存即同步（废除 `publish_campaign_rooms.py` 哈希烘焙）
* 全部 36 个章节房间直接作为人类可读、可随时打开修改的标准 Godot 场景存放在 `scenes/rooms/<room_id>.tscn`（根节点挂载 `RoomContainer3D.gd`）。
* 当人类在 Godot 编辑器（或游戏内 `F4` 建造台）按下 `Ctrl+S` 保存房间时，`RoomContainer3D.gd` 自动扫描场景内所有 `SmartFloorZone3D`、门洞（`HingedDoor3D`）与智能预制件（`PropEntity3D`）的坐标与 `dv_slot_id`，**1 秒内直接更新 `assets/data/presentation/campaign_rooms.dv` 与空间导航图**，完全省去过去导出候选 GLB、跑 45 秒渲染验收、再生成 `<sha256>.glb` 的繁琐流程。

---

## 四、全量文件功能映射与合并字典（File-by-File Mapping）

### 1. C# 核心层（原 256 个核心 `.cs` $\to$ 重构后 88 个高内聚 `.cs`）

#### 1.1 `core/dv_lang/`（原 `core/dv/` 3 文件 + `dv_core/compiler/` $\to$ 4 个文件）
| 目标文件路径 | 合并/迁移的源文件 | 核心职责与公开契约 |
| :--- | :--- | :--- |
| `core/dv_lang/DvScriptReader.cs` | 原 `core/dv/DvScriptReader.cs` | 手写递归下降解析器（最大深度 64），将 `ref`、`eq`、`all`、`assign` 转为 AST |
| `core/dv_lang/HistoryScriptReader.cs` | 原 `core/dv/HistoryScriptReader.cs` | 解析案情历史时间线与回溯声明块 |
| `core/dv_lang/DvDocument.cs` | 原 `core/dv/DvDocument.cs` | 多 `.dv` 文件合并、重名 ID 冲突拦截与全局 SHA-256 指纹计算 |
| `core/dv_lang/DvCompiler.cs` | 迁移自 `dv_core/compiler/DvCompiler.csproj` | 将 `.dv` AST 预编译为免反射静态 C# 模块（`.g.cs`） |

#### 1.2 `core/ontology/`（提炼自原 `core/engine/` 状态与存储相关文件 $\to$ 9 个文件）
| 目标文件路径 | 合并/迁移的源文件（原位于 `core/engine/`） | 核心职责与公开契约 |
| :--- | :--- | :--- |
| `core/ontology/Primitives.cs` | `Primitives.cs`, `Rational.cs`, `Models.cs` | 基础值类型、精确有理数运算（避免浮点非确定性）与核心实体 ID |
| `core/ontology/State.cs` | `State.cs`, `RecordStorage.cs`, `Memoization.cs` | 双本体状态树存储、`F_dv_cell` 结构定义与 `ReadCell` 跨主体越界拦截 |
| `core/ontology/References.cs` | `References.cs`, `Context.cs`, `Environment.cs` | 局部引用 `F_dv_local` 解析、求值上下文与作用域隔离 |
| `core/ontology/ValueOperations.cs` | `ValueOperations.cs`, `DvValueTemplate.cs`, `Predicates.cs`, `Conditions.cs` | AST 条件求值、算术/比较谓词执行与模板实例化 |
| `core/ontology/Arguments.cs` | `Arguments.cs`, `ArgumentValidation.cs`, `Requests.cs` | 动作参数绑定与类型/域合法性校验 |
| `core/ontology/ContentCatalog.cs` | `ContentCatalog.cs`, `ContentPolicy.cs`, `ContentTemplateQueries.cs` | 静态规则目录索引与内容访问安全策略 |
| `core/ontology/Snapshots.cs` | `Snapshots.cs`, `Lifecycle.cs` | 确定性状态快照克隆、回滚与哈希比对 |
| `core/ontology/Events.cs` | `Events.cs`, `Imprints.cs`, `CausalIndex.cs`, `CausalSupport.cs` | 因果事件记录、环境痕迹留存（Imprints）与因果支撑链索引 |
| `core/ontology/PresentationSpec.cs` | `Presentation.cs`, `Speech.cs` | 表现层元数据契约与语音/台词路由标识 |

#### 1.3 `core/cognition/`（提炼自原 `core/engine/` 感知与心智文件 + `core/runtime/PerceptionBindings.cs` $\to$ 7 个文件）
| 目标文件路径 | 合并/迁移的源文件 | 核心职责与公开契约 |
| :--- | :--- | :--- |
| `core/cognition/Perception.cs` | `engine/Perception.cs`, `runtime/PerceptionBindings.cs` | `F_dv_bind` 局部映射建立、授权集（`grants`）核验与 `F_dv_project_value` 投影 |
| `core/cognition/Awareness.cs` | `engine/Awareness.cs`, `engine/Appraisal.cs` | 环境扫描可见性判定与事件显著性评估 |
| `core/cognition/ActorCognition.cs` | `engine/ActorCognition.cs`, `engine/CognitiveClock.cs` | `F_dv_aggregate` 贝叶斯置信度刷新、`conflicted=true` 标记与 `F_dvs_erosion_update` 源质侵蚀记忆截断 |
| `core/cognition/BinaryEvidence.cs` | `engine/BinaryEvidence.cs`, `engine/EvidenceOutput.cs`, `engine/Factors.cs` | 证据因子权重计算与正/反双向证据条目生成 |
| `core/cognition/Explanations.cs` | `engine/Explanations.cs`, `engine/Prediction.cs` | 心智对异常现象的归因解释与状态预期推演 |
| `core/cognition/ConversationMind.cs` | `engine/ConversationExecution.cs`, `engine/Exchange.cs` | 对话中的信息交换、证言采信与说服交互逻辑 |
| `core/cognition/PersonalPolicy.cs` | `engine/PersonalPolicy.cs`, `engine/PlayerControl.cs` | 角色个人行为偏好策略与玩家接管控制权切换 |

#### 1.4 `core/reasoning/`（整合原 `core/reasoning/` 15 文件 + `core/engine/` 3 文件 $\to$ 6 个文件）
| 目标文件路径 | 合并/迁移的源文件 | 核心职责与公开契约 |
| :--- | :--- | :--- |
| `core/reasoning/CaseTheory.cs` | `reasoning/CaseTheory.cs`, `engine/Reasoning.cs` | 带符号霍恩逻辑（Signed Horn Logic）理论构建，显式建模 `Negative = true` |
| `core/reasoning/CaseProofChecker.cs` | `reasoning/CaseProofChecker.cs`, `engine/Proofs.cs`, `reasoning/CaseProofScript.cs` | 极小可信核心证明校验器，验证 `CaseProofNode` 拓扑偏序、变量绑定与指纹 |
| `core/reasoning/NaturalDeduction.cs` | `reasoning/NaturalDeduction.cs`, `reasoning/NaturalDeductionLean.cs` | 一阶直觉主义自然演绎推导与 Lean 形式化验证导出接口 |
| `core/reasoning/CaseHistory.cs` | `reasoning/CaseHistory.cs`, `CaseHistoryLean.cs`, `CaseHistoryProjection.cs`, `CaseHistoryReference.cs` | 案情时空历史重构与时间线矛盾检测 |
| `core/reasoning/CaseBranches.cs` | `reasoning/CaseBranches.cs`, `reasoning/CaseFiniteModel.cs`, `reasoning/CaseInferenceWork.cs` | 多假说分支派生与有限模型可满足性枚举 |
| `core/reasoning/CaseStrategy.cs` | `engine/CaseStrategy.cs`, `reasoning/StrategyJudgment.cs`, `reasoning/CaseTravel.cs`, `reasoning/CaseLeanBridge.cs` | 侦探调查策略评估与跨区域取证路径推理 |

#### 1.5 `core/autonomy/`（整合原 `core/reasoning/` 规划器与 `core/runtime/` 动机调度 $\to$ 5 个文件）
| 目标文件路径 | 合并/迁移的源文件 | 核心职责与公开契约 |
| :--- | :--- | :--- |
| `core/autonomy/AutonomyDriveSolver.cs` | `reasoning/AutonomyDriveSolver.cs`, `engine/AutonomyState.cs` | 凹性动机满意度求解器 $Satisfaction(v) = v - \frac{v^2}{200}$ 与边际效用排序 |
| `core/autonomy/DriveCalculator.cs` | `runtime/DriveCalculator.cs`, `runtime/DriveCandidateIndex.cs`, `runtime/PhysiologicalRoutes.cs` | 角色生理/心理需求衰减计算与候选满足动作索引 |
| `core/autonomy/Sim4Planner.cs` | `reasoning/Sim4Planner.cs`, `runtime/TaskPlanner.cs` | 结合动作耗时代价的 NPC 多步任务序列规划 |
| `core/autonomy/GoalPlanning.cs` | `runtime/GoalPlanning.cs`, `GoalPlanningAdapter.cs`, `GoalPlanningChecks.cs`, `StrategyActivity.cs` | 高层日程目标分解与阴谋活动（Social Schemes）介入调度 |
| `core/autonomy/DecisionScheduler.cs` | `runtime/DecisionScheduler.cs`, `engine/Decisions.cs`, `engine/Scheduler.cs`, `runtime/ActionFailureMemory.cs` | NPC 决策队列仲裁与失败动作冷却记忆 |

#### 1.6 `core/spatial/`（整合原 `core/runtime/`、`core/engine/` 与 `tools/building/SeatingFit.cs` $\to$ 5 个文件）
| 目标文件路径 | 合并/迁移的源文件 | 核心职责与公开契约 |
| :--- | :--- | :--- |
| `core/spatial/NavigationGraph.cs` | `runtime/NavigationGraph.cs`, `runtime/PathJourney.cs`, `engine/Navigation.cs`, `engine/NavigationTasks.cs` | 跨房间拓扑寻路、移动耗时计算与路径途经点生成 |
| `core/spatial/PortalPolicy.cs` | `runtime/PortalPolicy.cs`, `runtime/RoomVisibility.cs` | 门洞开关状态、视线穿透规则与房间可见性传播策略 |
| `core/spatial/RoomLifecycle.cs` | `runtime/RoomLifecycle.cs`, `runtime/RoomResidency.cs`, `runtime/RuntimeSpatial.cs` | 房间驻留人口追踪、动态加载生命周期与空间事件广播 |
| `core/spatial/SlotReservations.cs` | `runtime/SlotReservations.cs`, `runtime/SceneComposer.cs` | 家具/交互槽位（Slot）互斥占用锁与场景实体装配 |
| `core/spatial/SeatingFit.cs` | 迁移自 `tools/building/SeatingFit.cs` | **原生建造器实时校验核心**：验证智能预制件的 `ReachTarget3D` 站位点与坐席包围盒是否与墙体/邻近物体冲突 |

#### 1.7 `core/simulation/`（整合原 `core/runtime/` 与 `core/engine/` 步进与战役装配 $\to$ 8 个文件）
| 目标文件路径 | 合并/迁移的源文件 | 核心职责与公开契约 |
| :--- | :--- | :--- |
| `core/simulation/SimulationClock.cs` | `runtime/SimulationClock.cs`, `engine/FixedStepClock.cs` | 物理时长与逻辑时长双轨流速时钟及对话/睡眠唤醒边界控制 |
| `core/simulation/RuntimeSession.cs` | `runtime/RuntimeSession.cs`, `runtime/SingleStepPolicy.cs`, `engine/Simulation.cs`, `engine/SimulationEnvironment.cs` | 单步驱动 7 阶段切片流水线（位移 $\to$ 感知 $\to$ 规则 $\to$ 心智 $\to$ 提交 $\to$ NPC $\to$ 时钟） |
| `core/simulation/ExecutionEngine.cs` | `engine/Execution.cs`, `engine/ExecutionOutcomes.cs`, `engine/Eligibility.cs`, `engine/ActionCosts.cs`, `engine/Laws.cs` | 动作前置条件核验、代价扣除与原子状态提交 |
| `core/simulation/WorldRules.cs` | `runtime/WorldRules.cs`, `runtime/RuntimeMechanics.cs`, `runtime/Conditions.cs` | 环境机关触发、毒素扩散与全局物理规律演化 |
| `core/simulation/CampaignScenario.cs` | `runtime/CampaignScenario.cs`, `runtime/CampaignProgress.cs`, `runtime/SharedScenario.cs`, `runtime/CompactScenario.cs` | 五日战役（1440 分钟步进）装配与跨日 `.sleep_night.done` $\to$ `.wake` 因果链接 |
| `core/simulation/CharacterSessionDef.cs` | `runtime/CharacterSessionDefinition.cs`, `runtime/CharacterContentRevision.cs`, `runtime/ActorClient.cs` | 角色会话初始化定义与内容热重载版本比对 |
| `core/simulation/ConversationRuntime.cs` | `runtime/ConversationSession.cs`, `runtime/RuntimeNarrative.cs` | 运行时多角色对话回合状态机与叙事分支推进 |
| `core/simulation/RuntimeContent.cs` | `runtime/RuntimeContent.cs`, `runtime/ActionCatalog.cs`, `runtime/Values.cs`, `runtime/ValueTemplates.cs`, `runtime/SessionFactory.cs`, `runtime/RuntimeDebugSettings.cs` | 运行时动作目录构建与会话工厂 |

#### 1.8 `core/session_cli/`（整合原 `core/session/` 与 `core/cli/`，剥离硬编码战役文件 $\to$ 9 个文件）
* **关键剥离动作**：原 `core/session/` 下的 `ChapterOne.cs`、`ChapterTwo.cs`、`IslandCases.cs`、`IslandEnvironment.cs`、`IslandTopology.cs` 属于硬编码剧本内容，将其数据逻辑全部下沉转化为 `assets/data/scenarios/campaign.dv` 规则声明，从 C# 核心库中移除。

| 目标文件路径 | 合并/迁移的源文件 | 核心职责与公开契约 |
| :--- | :--- | :--- |
| `core/session_cli/Session.cs` | `session/Session.cs`, `session/SessionInFlight.cs`, `session/SessionCompletion.cs`, `session/KernelImports.cs` | `DvSession` 统一门面，实现 `PrepareValue`、`PrepareAhead` 投机执行与 `CompleteValue` |
| `core/session_cli/ObserverProjection.cs` | `session/ObserverProjection.cs`, `session/CampaignProjection.cs`, `session/HostValues.cs`, `session/AuthoredValues.cs` | 观察者视角过滤与 `HostStateDelta` 差量状态投影生成 |
| `core/session_cli/SessionCaseApi.cs` | `session/SessionCases.cs`, `SessionCaseMindEvidence.cs`, `SessionCaseQuestions.cs`, `SessionCaseRequests.cs`, `SessionCaseStrategy.cs`, `SessionCaseScene.cs`, `SessionCaseNavigation.cs`, `SessionEvidence.cs`, `SessionNaturalDeduction.cs` | 面向 UI 的案卷查询、推演提交、疑点提问与证据比对接口 |
| `core/session_cli/SessionActionApi.cs` | `session/CampaignActionDispatch.cs`, `SessionSpatialActions.cs`, `SessionLocomotion.cs`, `SessionSim4.cs`, `CandidateRanking.cs` | 空间移动、交互动作派发与候选动作排序接口 |
| `core/session_cli/CampaignSessionHost.cs` | `session/CampaignSession.cs`, `CampaignFlow.cs`, `CampaignConfiguration.cs`, `CampaignContent.cs`, `CampaignDefinitions.cs`, `CampaignDataSelections.cs`, `CampaignAdoptionCatalogs.cs`, `CampaignMovementBridges.cs`, `CampaignNavigationFrontier.cs` | 战役级会话状态流转、章节结算与跨区域导航边界管理 |
| `core/session_cli/SessionReplay.cs` | `session/SmartReplay.cs`, `session/SessionPerformance.cs`, `session/HostBuildIdentity.cs`, `session/HostContentSelection.cs`, `session/SessionContent.cs`, `session/AuthoredSceneExtensions.cs` | 智能存档回放、构建指纹校验与性能计时 |
| `core/session_cli/Program.cs` | `cli/Program.cs`, `cli/NativeCli.cs` | CLI 命令行入口与 JSON-RPC 交互主循环 |
| `core/session_cli/AuditAndTranscript.cs` | `cli/AppendAuditLog.cs`, `cli/CliTranscript.cs`, `cli/ReadableEventLog.cs` | 不可篡改的 `actions.yaml` 因果审计日志与人类可读战报生成 |
| `core/session_cli/VerificationSuites.cs` | `cli/DriveCalculatorChecks.cs`, `cli/RoomBundleChecks.cs`, `cli/RoomLifecycleChecks.cs`, `cli/CaseProofAuthoring.cs`, `cli/SpatialPublication.cs`, `session/SpatialPublication.cs` | 内置核心自检套件（`--verify-character-actions`, `--verify-rooms` 等） |

---

### 2. Godot C# 桥接与组件层（原 7 个 `.cs` $\to$ 6 个 `.cs`）

| 目标文件路径 | 源文件路径 | 迁移与改造说明 |
| :--- | :--- | :--- |
| `scenes/main/SessionBridge.cs` | 合并 `CharacterSessionBridge.cs` + `GameEngine.cs` | **核心合并**：废除 `GameEngine.cs` 中指向已删除 `assets/data/world.dv` 的旧路径，统一使用 `CharacterSessionBridge` 的原生角色会话模型作为唯一桥接器，并向 `godot_architect` 暴露 `ValidatePrefabPlacement()` 接口（内部调用 `SeatingFit.cs`） |
| `scenes/main/DvPresentationContent.cs` | 原 `scenes/main/DvPresentationContent.cs` | 保持不变，提供 `.dv` 表现层配置的 Godot 字典缓存 |
| `scenes/main/GodotHostValues.cs` | 原 `scenes/main/GodotHostValues.cs` | 保持不变，负责 C# 对象与 Godot Variant 零 GC 转换 |
| `scenes/main/CasebookPersistence.cs` | 原 `scenes/main/CasebookPersistence.cs` | 保持不变，负责案卷手写批注与推理图存档持久化 |
| `scenes/components/architecture/RoomStreamingController.cs` | 原路径不变 | 保持不变，负责房间流式加载与门洞多边形视锥穿透剔除 |
| `scenes/components/architecture/CutawayGeometry.cs` | 原路径不变 | 保持不变，由 `SmartWall3D.gd` 动态调用，实时计算拖拽墙体的剖切封口几何 |

---

### 3. Godot UI 体系（原 `scenes/UIpanel/` 44 对 `.tscn`/`.gd` $\to$ 12 对）

通过“**1 个书本外壳 + 4 套数据驱动双页模板 + 5 套现场/剧场 UI + 2 套全局基础组件**”，彻底消除 32 对重复的列表、边距与子页面脚本：

| 序号 | 目标场景与脚本路径（1 `.tscn` : 1 `.gd`） | 吸收合并的原 UI 文件（共 44 对） | 职责与架构改造要点 |
| :---: | :--- | :--- | :--- |
| **01** | `scenes/UIpanel/BookShell/BookShellUI.{tscn,gd}` | `CasebookUI`, `PhysicalBookView`, `NativeBookInterface`, `BookPageLayout`, `ManuscriptMargin`, `ManuscriptControls`, `UISystem`, `UITransition`, `CasebookData` | 统一书本容器。保留 `PaperViewport` ($1600 \times 1040$) 3D 弯曲纸面射线反投影；**新增 `F2` 键切换平面 2D 直通模式**，方便人类直接调试 UI 布局 |
| **02** | `scenes/UIpanel/Spreads/CatalogInspectSpread.{tscn,gd}` | `EvidenceArchiveUI` (B01), `RulebookUI` (B02), `ConventionUI` (B04) 及子组件 `EvidenceInspectUI`, `InventoryUI`, `InvestigationUI`, `TextComparisonUI` | **通用左索引+右检视模板**：通过传入 `mode = "evidence" \| "rulebook" \| "convention"` 动态绑定数据源，内置证物 3D 检视与双文本比对槽 |
| **03** | `scenes/UIpanel/Spreads/ChronicleMapSpread.{tscn,gd}` | `EventScheduleUI` (B03), `CampusMapPanel`, `CollaborationUI` | **通用日程矩阵+地图联动模板**：左页展示角色时间轴矩阵，右页展示学院矢量地图与同伴协作指派 |
| **04** | `scenes/UIpanel/Spreads/PinboardPlateSpread.{tscn,gd}` | `HypothesisNoteUI` (B05), `GalleryPlateUI` (B06) | **通用自由图版与笔记模板**：支持双页跨页画布、线索卡片钉板连线、手写批注与高清图版检视 |
| **05** | `scenes/UIpanel/Spreads/LedgerSystemSpread.{tscn,gd}` | `BookArchiveUI` (B07), `BookArchivePage`, `BookSettingsSpread` (B08), `BookSettingsPage`, `ReplayPreferences` | **通用账本存档与设置模板**：左页存档槽位/设置分类，右页智能回放参数与键位/画质表单 |
| **06** | `scenes/UIpanel/Field/FieldHUD.{tscn,gd}` | `FieldHUD`, `FieldChrome`, `CurrentActionUI`, `ActionQueuePanel`, `TooltipGuideUI`, `Snackbar` | **常驻探案 HUD**：将动作队列、当前执行进度条、按键提示与顶部轻提示（Snackbar）收拢为单场景内部节点树 |
| **07** | `scenes/UIpanel/Field/InteractionMenu.{tscn,gd}` | `InteractionMenu` | **3D 目标切角交互短签**：锚定于 3D 交互物旁的上下文动作抽屉 |
| **08** | `scenes/UIpanel/Field/DeductionInterface.{tscn,gd}` | `DeductionInterface`, `ActionOrderPanel`, `DeductionVisual3D` | **蓝图风格脑内推演空间**：连接 `SessionCaseApi.cs`，可视化编辑与校验霍恩逻辑证明树 |
| **09** | `scenes/UIpanel/Field/DialogueUI.{tscn,gd}` | `DialogueUI` | **双角色对话剧场**：管理立绘差分、证言呈堂、追问打断与语音播放 |
| **10** | `scenes/UIpanel/Field/TitleStageUI.{tscn,gd}` | `TitleUI`, `TitleSpread`, `StageSettlementUI` | **胡桃木书案标题与章节结算界面**：支持开场书案状态与每日结束的阶段结算报告 |
| **11** | `scenes/UIpanel/Shared/ModalAlertUI.{tscn,gd}` | `ModalPanel`, `AlertUI` | **全局模态确认框与源质侵蚀警报叠层** |
| **12** | `scenes/UIpanel/Shared/UIInput.{tscn,gd}` | `UIInput` | **统一输入路由器**：管理快捷键、手柄/键鼠焦点及 `F2`（平面UI）/`F3`（认知透视）/`F4`（3D房间建造台）调试热键分发 |

---

### 4. 3D 建筑结构组件、智能预制件与房间场景映射（核心重构区）

#### 4.1 `scenes/components/architecture/`（原 36 对碎片脚本 $\to$ 收敛为 10 个 `@tool` 参数化建造与核心交互组件）
| 目标文件路径 | 吸收合并的原脚本 | 核心职责与人类操作特性 |
| :--- | :--- | :--- |
| `SmartWall3D.{tscn,gd}` | `WallSurface3D`, `SkirtingBoard3D`, `CutawayCap3D`, `WallOcclusionController`, `OpeningFrame3D`, `DoorFrameClearance`, `StructuralMember3D` | **`@tool` 点击拖拽墙体**：支持网格吸附拉伸、自动踢脚线与剖切封口、拖入 `HingedDoor3D`/`HingedWindow3D` 时自动在墙体上打洞并生成门窗框 |
| `SmartRailing3D.{tscn,gd}` | `CurveRailing3D`, `RailingAssemblyRenderer`, `RailingHandrail3D` | **`@tool` 路径点点击栏杆**：在视口点击生成折线或曲线，自动排布立柱、转角柱与连续扶手网格 |
| `SmartStair3D.{tscn,gd}` | `StraightStair3D`, `ChamferGeometry` | **`@tool` 参数化楼梯**：拖拽起点与上层终点控制柄，自动计算踏步高度并联动生成两侧 `SmartRailing3D` |
| `SmartFloorZone3D.{tscn,gd}` | `FloorSurface3D`, `CeilingSurface3D`, `RoomGrid`, `RoomArtFinish`, `RoomTerrainSkin`, `AuthoredTerrainBorder`, `TerrainArcade` | **`@tool` 多边形地面与房间边界**：点击围合成房间地板与参考网格，自动计算门洞视锥剔除包围盒 |
| `RoomContainer3D.gd` | `BuildingScene`, `BuildingFramework`, `BuildingProjectResource`, `ArchitectureController`, `RoomKnowledgeView` | **36 个房间场景的唯一根节点脚本**：管理房间内结构件与智能预制件列表，保存场景时自动将槽位与门洞拓扑同步写入 `campaign_rooms.dv` |
| `PropEntity3D.gd` | `BuildingInteractionComponent`, `InteractiveProp` 及原 286 个 `<sha256>.gd` | **智能预制件通用根脚本**：管理预制件的 `AnimationPlayer` 动作触发、`ReachTarget3D` 站位锚点查询、`dv_slot_id` 绑定与四象限手绘着色器参数 |
| `HingedDoor3D.{tscn,gd}` | `HingedDoor3D`, `ArchitecturalDoor`, `BuildingDoor` | **带开关动画与门洞视锥绑定的标准门基类**：当吸附到 `SmartWall3D` 上时自动切开墙体并注册 `Portal` |
| `HingedWindow3D.{tscn,gd}` | `HingedWindow3D` | **带开关动画与采光锥绑定的标准窗基类**：吸附到 `SmartWall3D` 上时自动切开窗洞 |
| `CircularBlastDoor3D.{tscn,gd}` | `CircularBlastDoor3D`, `ValveAssembly3D` | **工业区/地下室重型圆形气密门与阀门转动机关专用组件** |
| `ExteriorRoof3D.{tscn,gd}` | `ExteriorRoof3D`, `ExteriorArchitecture`, `ReferenceFacade`, `YiArchitecture` | **建筑外立面与屋顶参数化组件** |

#### 4.2 `scenes/prefabs/` 智能预制件库（原 `prefabs/` 58 个 `.tscn` + 286 个哈希构件去重归类 $\to$ 约 85 个标准 `.tscn` 智能预制件）
* **彻底废除哈希命名与孤儿脚本**：删除 `scenes/components/prefabs/` 下全部 134 个独立 `.gd` 脚本，以及 `assets/scenes/campaign_rooms/components/` 下全部 286 对 `<sha256>.{tscn,gd}`。
* **建立人类可读分类目录**：将底层原子级 `.glb` 模型统一归入 `assets/meshes/`，并在 `scenes/prefabs/` 下按 `doors_windows/`、`furniture/`、`interactive_props/`、`lighting_decor/` 封装为约 85 个带人类可读名称（如 `VintageWritingDesk.tscn`, `LaboratoryCentrifuge.tscn`, `DoubleArchDoor.tscn`）的**已配置好动作、碰撞、站位点与特性的 `.tscn` 智能预制件**。

#### 4.3 `scenes/rooms/` 正式房间场景（原 `assets/scenes/campaign_rooms/rooms/` 36 对）
* 将 36 个正式房间 `.tscn` 迁移至 `scenes/rooms/<room_id>.tscn`，删除其同名的 36 个冗余 `.gd` 脚本，根节点统一挂载 `RoomContainer3D.gd`。
* 房间内部结构由 `SmartWall3D`、`SmartRailing3D`、`SmartStair3D`、`SmartFloorZone3D` 构成，室内摆件全部直接实例化（Instance）自 `scenes/prefabs/*.tscn` 智能预制件，人类随时可以在 Godot 中点开任意房间，直接拖拽墙体、拉动栏杆节点或摆放已带好动作的家具！

#### 4.4 `tools/godot_architect/` 原生 3D 房间建造器工具集（新增 4 个文件，替代外部 Three.js 建筑编辑器与离线 Blender 房间生成脚本）
| 目标文件路径 | 核心职责与功能说明 |
| :--- | :--- |
| `tools/godot_architect/ArchitectPlugin.gd` | Godot `@tool` EditorPlugin 入口，在编辑器顶部与侧边栏挂载结构画笔工具栏与智能预制件可视化面板 |
| `tools/godot_architect/ArchitectGizmo3D.gd` | 3D 视口交互手柄：提供墙体起点/终点拖拽柄、栏杆路径点增删手柄、楼梯高度拉伸柄以及门窗吸附预览框 |
| `tools/godot_architect/PrefabPaletteDock.{tscn,gd}` | **智能预制件可视化选件面板**：带缩略图展示 `scenes/prefabs/` 中所有已配置好动作和特性的物体，支持过滤、悬停 3D 预览、`R` 键旋转、实时 `SeatingFit.cs` 站位合法性绿/红脚印指示，以及 `.dv` 逻辑槽位一键绑定 |
| `tools/godot_architect/LiveBuilderOverlay.{tscn,gd}` | **游戏内 `F4` 实时建造模式**：允许人类在运行游戏操控 3.5 头身侦探走动的同时，按 `F4` 随时拉起一面墙、画一段栏杆或拖入一张带动画的书桌，并立即操控角色走过去测试动作！ |

---

## 五、物理清剿黑名单（Complete Deletion Manifest）

在迁移阶段一与阶段二，直接通过 `git rm -rf` 物理删除以下路径，共计清理 **约 1,550 个冗余/废弃/机器生成的黑盒文件**：

```text
# 1. 废弃的独立旧版工程与重复核心 (517 个文件)
godot_csharp/                                  # 432 个文件 (163 .gd, 264 .tscn, 4 .cs, 1 .html)
dv_core/Core/                                  # 53 个旧版 .cs 文件
dv_core/Session/                               # 30 个旧版 .cs 文件
dv_core/Cli/                                   # 2 个旧版 .cs 文件

# 2. 暂存区、归档碎片与根目录临时脚本 (28 个文件)
scenes_temp/                                   # 8 个文件 (4 .gd, 4 .tscn)
artifacts/                                     # 19 个文件 (4 .py, 11 .gd, 4 .tscn)
scratch_yaml_test.js                           # 1 个临时 JS 脚本

# 3. 已被原生房间替代的旧版地图切片与实验场 (311 个文件)
scenes/components/maps/                        # 49 个文件 (25 .gd, 24 .tscn)
scenes/components/maps3d/                      # 130 个文件 (81 .gd, 49 .tscn)
scenes/components/world/CastE.*                # 及同目录下其余 47 对早期测试场景 (96 个文件)
scenes/components/prefabs/*.gd                 # 134 个孤儿家具脚本（58 个 .tscn 升级为标准智能预制件）

# 4. 机器哈希发布的黑盒构件与旧离线整房生成管线 (609 个文件)
assets/scenes/campaign_rooms/components/       # 572 个文件 (286 对 <sha256>.tscn 与 <sha256>.gd，替换为可读的 scenes/prefabs/*.tscn)
assets/scenes/campaign_rooms/rooms/*.gd        # 36 个房间重复脚本（房间 .tscn 迁入 scenes/rooms/ 并挂载通用 RoomContainer3D.gd）
tools/building/publish_campaign_rooms.py       # 废除哈希烘焙发布脚本（改由 RoomContainer3D.gd 保存即同步）
tools/building/compose_courtyard_rooms.py      # 废除离线代码拼房脚本
tools/building/lowpoly_chapters_v3/kit/*_scenes.py # 废除用 Python 盲写房间坐标的脚本（保留原子家具导出器供制作底层 .glb）
tools/web_building_editor/                     # 废除只能摆裸模型的外部 Three.js 建筑编辑器（由 tools/godot_architect/ 原生替代）

# 5. 硬编码于引擎层的特定剧本 C# 文件 (5 个文件，内容转入 .dv)
core/session/ChapterOne.cs
core/session/ChapterTwo.cs
core/session/IslandCases.cs
core/session/IslandEnvironment.cs
core/session/IslandTopology.cs
```

---

## 六、分步迁移实施手册（Step-by-Step Migration Playbook）

整个迁移过程严格拆分为 **6 个原子级提交阶段（Atomic Phases）**。每个阶段结束时必须运行对应的验证命令，只有校验 100% 通过才允许进入下一阶段。

### Phase 0：固化黄金基准快照（Golden Baseline Generation）
在修改任何一行代码前，采集当前正式入口的确定性基线数据：
1. 运行 CLI 全战役回归校验并导出因果审计流与状态指纹：
   ```bash
   dotnet run --project core/cli/DuelView.Cli.csproj -- --verify-character-actions --export-golden tests/golden/campaign_baseline.json
   ```
2. 运行故事证明核验器，记录所有案件的 `CaseProofChecker` 拓扑签名：
   ```bash
   uv run python tools/story/author.py --scenario assets/data/scenarios/campaign.dv --dump-proofs tests/golden/proofs_baseline.json
   ```
3. 运行 Godot 45 秒自动化冒烟测试，保存基准帧截图与 `actions.yaml` 回执。

### Phase 1：死代码与废弃目录物理清剿（Zero-Risk Purge）
1. 执行物理删除：
   ```bash
   git rm -rf godot_csharp dv_core/Core dv_core/Session dv_core/Cli scenes_temp artifacts scratch_yaml_test.js
   git rm -rf scenes/components/maps scenes/components/maps3d
   ```
2. 将 `dv_core/compiler/` 移动至 `core/dv_lang/compiler/`，然后删除空的 `dv_core/` 目录。
3. 编辑 `.gdignore`：移除对 `godot_csharp` 的忽略项。
4. 编辑 `project.godot`：移除 `[file_customization]` 下对 `scenes_temp/` 的颜色标记。
5. 合并 `scenes/main/GameEngine.cs` 与 `CharacterSessionBridge.cs` 为 `scenes/main/SessionBridge.cs`，删除所有对 `res://assets/data/world.dv` 的字面引用。
6. **阶段门禁核验**：编译 `DuelView.sln`，运行 Phase 0 的 `campaign_baseline.json` 对比测试，确保 SHA-256 指纹 **100% 一致**。

### Phase 2：构建智能预制件库与 Godot 原生 3D 房间建造器（Native 3D Architect & Smart Prefabs）
1. **升级参数化建筑组件（`scenes/components/architecture/`）**：
   * 实现 `@tool` 脚本 `SmartWall3D.gd`（支持点击拖拽拉伸与门窗自动开洞）、`SmartRailing3D.gd`（支持 `Path3D` 路径点点击自动排布立柱与扶手）、`SmartStair3D.gd`（支持高度拖拽与侧栏杆派生）、`SmartFloorZone3D.gd`（多边形地板与视锥边界）。
2. **构建已配置好动作与特性的智能预制件库（`scenes/prefabs/`）**：
   * 将去重后的底层原子模型存入 `assets/meshes/`；
   * 在 `scenes/prefabs/` 下建立 `doors_windows/`、`furniture/`、`interactive_props/`、`lighting_decor/`，为每个 `.tscn` 统一挂载 `PropEntity3D.gd`（或 `HingedDoor3D.gd`），配置好 `AnimationPlayer` 动作、`ReachTarget3D` 角色站位锚点、`SeatPoseModifier3D` 坐姿偏移、物理碰撞盒与手绘着色器参数。
3. **迁移 36 个房间至 `scenes/rooms/*.tscn` 并接入 `tools/godot_architect/`**：
   * 将 36 个房间的引用从旧 `<sha256>.tscn` 替换为 `scenes/prefabs/*.tscn` 智能预制件与 `SmartWall3D`/`SmartRailing3D` 结构节点，根节点挂载 `RoomContainer3D.gd`；
   * 启用 `tools/godot_architect/` 编辑器插件与游戏内 `F4` 实时建造台，接入 `SeatingFit.cs` 实时站位红绿脚印检测；
   * 物理删除 `assets/scenes/campaign_rooms/components/` 下的 286 对哈希文件及离线发布脚本 `publish_campaign_rooms.py`。
4. **阶段门禁核验**：
   ```bash
   dotnet run --project core/cli/DuelView.Cli.csproj -- --verify-room-bundles
   uv run python mcp/spatial/audit_ig01.py
   ```

### Phase 3：C# 核心 8 模块重组与剧本解耦（Core Consolidation）
1. 将 `ChapterOne.cs`、`ChapterTwo.cs`、`Island*.cs` 中的静态初始化配置提取为 `assets/data/scenarios/` 下的 `.dv` 块，删除上述 5 个 C# 文件。
2. 按照 **第四节第 1 部分** 的映射表，在 `core/` 下建立 `dv_lang`、`ontology`、`cognition`、`reasoning`、`autonomy`、`spatial`、`simulation`、`session_cli` 8 个子目录及对应的 `.csproj` 文件，按 **第二节第 1 部分** 的 DAG 矩阵配置 `<ProjectReference>`。
3. 在每个整理后的 `.cs` 文件顶部写入强制性的 3 行机制元数据契约：
   ```csharp
   // @mechanism: <mechanism_id>
   // @layer: core/<module_name>
   // @contracts: <KeyClassOrMethod>
   ```
4. **阶段门禁核验**：运行 `dotnet build DuelView.sln --configuration Release` 确认零警告，并再次对比 `tests/golden/campaign_baseline.json` 与 `proofs_baseline.json` 确保逻辑零偏移。

### Phase 4：UI 体系模板化合并与 `F2` 平面直通模式（UI Simplification）
1. 在 `scenes/UIpanel/BookShell/BookShellUI.gd` 中实现双渲染管线切换：
   * 默认模式（`use_3d_book_stage = true`）：将 `SpreadContainer` 挂载于 `PaperViewport` 下，通过 `LowpolyBookStage3D.gd` 执行射线 UV 反投影；
   * 平面创作模式（按 `F2` 切换 `use_3d_book_stage = false`）：将 `SpreadContainer` 直接挂载至根 `CanvasLayer` 全屏居中显示，旁路 3D 视口转换。
2. 按照 **第四节第 3 部分** 依次实现 `CatalogInspectSpread`、`ChronicleMapSpread`、`PinboardPlateSpread`、`LedgerSystemSpread` 4 套模板，并在 `F2` 平面模式下逐一核对 B01 $\sim$ B08 的数据绑定。
3. 将 `FieldHUD` 及其子组件内聚为单一场景，验证通过后执行 `git rm` 删除原 32 对旧 UI 面板文件。
4. **阶段门禁核验**：运行 MCP UI 自动化巡检脚本验证全部书页翻页、证据检视与推演连线。

### Phase 5：部署剧情时间线与证据推演可视化工作台（DuelView Studio Workbench）
1. **构建 `tools/studio_web/` 剧情与推演画布**：
   * **`timeline_gantt.html`**：读取 `assets/data/scenarios/life-*.dv` 与 `social-schemes.dv`，渲染 5 天 $\times$ 9 角色的甘特图泳道，支持拖拽调整作息时间窗并自动校验跨日 `.sleep_night.done` $\to$ `.wake` 锁链，点击任一活动块可直接跳转到对应的 `scenes/rooms/<room_id>.tscn` 交互槽位。
   * **`proof_dag.html`**：连接 `DuelView.Cli` 后台服务，以可视化有向无环图（DAG）展示物理事实（蓝）、主观观察（黄）、反向证据（红），连线时实时触发 `CaseProofChecker` 校验。
2. **创建 Godot 内嵌调试沙盒 `tools/sandbox/AuthoringSandbox.tscn` 与 `DebugOverlay3D.gd`**：
   * 绑定 `F3` 热键，在 3D 视口内实时绘制 NPC `intersect_ray` 视线锥、门洞剔除平面，以及角色头顶当前 `AutonomyDriveSolver` 排名前三的动机效用条。

### Phase 6：启用 `MECHANISM_MAP.yaml` 与永久防腐 CI 门禁
1. 在仓库根目录生成 `MECHANISM_MAP.yaml`（完整内容见第七节）。
2. 部署 `tools/ci/verify_repo_hygiene.py` 并将其接入 `.git/hooks/pre-commit` 与 `mcp/game/runtime.py` 的 `build_gate` 前置检查中。

---

## 七、AI 机制定位索引与防腐门禁实现规范

### 1. 根目录 `MECHANISM_MAP.yaml` 标准配置
此文件固化于仓库根目录，供 AI 与人类开发者在 1 次读取内定位任何游戏机制的完整代码链路：

```yaml
version: "2.1"
entry_points:
  godot_main: "scenes/main/Main.tscn"
  csharp_session_bridge: "scenes/main/SessionBridge.cs"
  cli_host: "core/session_cli/Program.cs"
  default_scenario: "assets/data/scenarios/campaign.dv"

mechanisms:
  dual_ontology_state:
    description: "物理客观域与角色主观心智域双本体状态隔离及跨主体访问拦截"
    dv_keywords: ["domain", "subject", "physical", "mind", "F_dv_cell"]
    csharp_authority:
      - "core/ontology/State.cs"
      - "core/ontology/References.cs"
    godot_presenters:
      - "scenes/main/SessionBridge.cs"
    verify_cmd: "dotnet run --project core/session_cli/DuelView.Cli.csproj -- --verify-ontology"

  perception_and_erosion:
    description: "局部绑定投影、贝叶斯心智置信度聚合与源质侵蚀记忆周期截断"
    dv_keywords: ["F_dv_bind", "F_dv_perceive", "F_dv_aggregate", "F_dvs_erosion_update", "grants"]
    csharp_authority:
      - "core/cognition/Perception.cs"
      - "core/cognition/ActorCognition.cs"
    godot_presenters:
      - "scenes/UIpanel/Field/FieldHUD.gd"
      - "scenes/UIpanel/Shared/ModalAlertUI.gd"
    verify_cmd: "dotnet run --project core/session_cli/DuelView.Cli.csproj -- --verify-cognition"

  signed_horn_reasoning:
    description: "带符号霍恩逻辑、显式负证据与极小可信核心证明树校验"
    dv_keywords: ["theory", "proof", "negative", "hypothesis"]
    csharp_authority:
      - "core/reasoning/CaseTheory.cs"
      - "core/reasoning/CaseProofChecker.cs"
      - "core/reasoning/NaturalDeduction.cs"
    godot_presenters:
      - "scenes/UIpanel/Field/DeductionInterface.gd"
      - "scenes/UIpanel/Spreads/PinboardPlateSpread.gd"
    workbench_tool: "tools/studio_web/proof_dag.html"
    verify_cmd: "uv run python tools/story/author.py --scenario assets/data/scenarios/campaign.dv"

  autonomy_drive_solver:
    description: "凹性满意度动机求解器与 Sim4 NPC 行为规划"
    dv_keywords: ["drive", "satisfaction", "routine", "scheme"]
    csharp_authority:
      - "core/autonomy/AutonomyDriveSolver.cs"
      - "core/autonomy/DriveCalculator.cs"
      - "core/autonomy/Sim4Planner.cs"
    godot_presenters:
      - "scenes/components/characters/AutonomousActorExecutor.gd"
      - "tools/sandbox/DebugOverlay3D.gd"
    workbench_tool: "tools/studio_web/timeline_gantt.html"
    verify_cmd: "dotnet run --project core/session_cli/DuelView.Cli.csproj -- --verify-drives"

  causal_clock_and_campaign:
    description: "双轨流速因果时钟、7阶段单步切片流水线与五日战役跨日因果锁链"
    dv_keywords: ["campaign", "sleep_night", "wake", "content_time"]
    csharp_authority:
      - "core/simulation/SimulationClock.cs"
      - "core/simulation/RuntimeSession.cs"
      - "core/simulation/CampaignScenario.cs"
    godot_presenters:
      - "scenes/UIpanel/Spreads/ChronicleMapSpread.gd"
      - "scenes/components/rendering/WorldTimeLighting.gd"
    verify_cmd: "dotnet run --project core/session_cli/DuelView.Cli.csproj -- --verify-character-actions"

  native_3d_architect_and_prefabs:
    description: "Godot 原生点击拖拽参数化建筑结构 (墙壁/栏杆/楼梯) 与已配好动作的智能预制件摆放"
    dv_keywords: ["portal", "room", "slot"]
    csharp_authority:
      - "core/spatial/PortalPolicy.cs"
      - "core/spatial/RoomLifecycle.cs"
      - "core/spatial/SeatingFit.cs"
      - "scenes/components/architecture/RoomStreamingController.cs"
      - "scenes/components/architecture/CutawayGeometry.cs"
    godot_presenters:
      - "scenes/components/architecture/SmartWall3D.gd"
      - "scenes/components/architecture/SmartRailing3D.gd"
      - "scenes/components/architecture/SmartStair3D.gd"
      - "scenes/components/architecture/SmartFloorZone3D.gd"
      - "scenes/components/architecture/RoomContainer3D.gd"
      - "scenes/components/architecture/PropEntity3D.gd"
    workbench_tool: "tools/godot_architect/PrefabPaletteDock.tscn"
    verify_cmd: "dotnet run --project core/session_cli/DuelView.Cli.csproj -- --verify-room-bundles"

  book_viewport_projection:
    description: "SubViewport 3D 弯曲书页射线 UV 反投影与 F2 平面直通双模式"
    dv_keywords: ["casebook", "spread"]
    csharp_authority:
      - "scenes/main/CasebookPersistence.cs"
    godot_presenters:
      - "scenes/UIpanel/BookShell/BookShellUI.gd"
      - "scenes/components/world/LowpolyBookStage3D.gd"
      - "assets/shaders/book_leaf_mesh_3d.gdshader"
    verify_cmd: "uv run python mcp/verification/verify_batch1.py"
```

### 2. 仓库卫生与架构防腐脚本（`tools/ci/verify_repo_hygiene.py`）核心检查项
此脚本在每次构建与 Git 提交前自动运行，包含以下 **6 条硬性阻断规则**：
1. **黑名单目录阻断**：若检测到 `godot_csharp`、`dv_core`、`scenes_temp`、`artifacts`、`scenes/components/maps`、`scenes/components/maps3d`、`tools/web_building_editor` 存在，立即返回非零退出码并阻断构建。
2. **`assets/` 纯净度守卫**：扫描 `assets/` 全目录，若发现任何 `.gd`、`.cs` 或 `.tscn` 文件（防止 `publish_campaign_rooms.py` 死灰复燃生成 `<sha256>.gd`），立即报错阻断。
3. **房间禁止直接引用裸 `.glb` 守卫**：扫描 `scenes/rooms/*.tscn` 中的 `ext_resource`，强制要求所有室内物体必须引用自已配置好动作与交互锚点的 `res://scenes/prefabs/**/*.tscn` 智能预制件或 `res://scenes/components/architecture/*.tscn` 参数化结构件，**严禁在房间场景中直接拖入未配置的 `.glb` 裸网格**。
4. **1:1 场景配对与无孤儿脚本守卫**：扫描 `scenes/UIpanel/`，强制核验 `.tscn` 与 `.gd` 数量严格不大于 12 对且 1:1 同名配对；扫描 `scenes/prefabs/`，确保其下 `.tscn` 全部挂载通用 `PropEntity3D.gd` 或标准门窗脚本，禁止散落孤儿 `.gd` 脚本。
5. **`res://` 死链静态扫描**：使用正则提取全仓 `.cs`、`.gd`、`.tscn`、`.dv` 中的所有 `res://...` 路径字面量，验证对应物理文件是否存在，彻底杜绝类似 `res://assets/data/world.dv` 的残留死链。
6. **`@mechanism` 契约双向核验**：扫描 `core/` 下所有 `.cs` 文件的头部 `@mechanism:` 标签，确认其全部在 `MECHANISM_MAP.yaml` 中合法存在，且 `MECHANISM_MAP.yaml` 中列出的所有文件路径均真实存在。