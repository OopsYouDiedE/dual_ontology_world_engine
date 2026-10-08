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

---

## 四、工具与插件管理

- `tools/`：素材批量切片、图集生产、场景组装及 Godot 命令行测试工具。
- `addons/<plugin_name>/`：编辑器插件（如素材适配器 `rpg_asset_adapter`），代码与资源保持插件内部封闭。
- 资产迁移或管线变动后，通过 `& $env:GODOT --headless --editor --path godot_csharp --import` 完成重新导入校验。
- 一级验收的 `capture_player_transcript.gd` 以正式 UI 实际绘制的文字、物品和末个终局为验收依据；截图只作辅助，不以截图数量或齐全与否阻止导出 03。辅助截图按正式视图、事件类型与结构化字段识别，不绑定 dispatch 序号或中文台词。
- 一级回放默认不截图、不要求布局图审阅；仅在显式传入 `--screenshots` 时收集辅助图片，此时须先查看 `draw_player_transcript_layout.py` 的规划图并传入 `--layout-reviewed`。二级 UI 专项仍须实际截图查看。

- `PlayerTranscript` 的正常路线在同一日同一时段不重复询问同一人物；时段变化后可以再问。此选择仅减少验收路线的重复操作，不改变正式游戏允许的询问次数。
- 议事立场预览与正式方案共用剧本规则定义（作者、文本、标记），不得在预览代码保留另一套剧情方案或来源。
