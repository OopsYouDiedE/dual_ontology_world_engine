# Godot 游戏宿主维护与路径规范

本目录为 Godot 4.6.1 .NET 游戏宿主，负责场景树、渲染、空间与碰撞、物理与输入、UI 及音效呈现。通过 ProjectReference 单向引用 Core；Godot 类型、运行时专属状态和素材完全由本目录管理。设计与验收标准详见 [统一设计规范](../docs/design-standard.md)。

**同步修改规则**：本目录发生架构、模块合同、命令或排错方式调整时，必须同步修改本文件（`godot_csharp/AGENTS.md`）。

## 测评方式约束

- 严格遵循根目录用户裁定：禁止执行流程回归（包括 `flow`、`flow2`、`flow_check.gd`、`check_flow.gd` 及统一回归调用）。
- 禁止以“xx 项通过”、断言数量、通过率或“全绿”评估游戏、人物或修改效果；所有机制检查仅用于定位具体工程问题，不构成验收依据。
- 验收必须使用新的正式游戏会话，逐段核对玩家实际可见信息、真实输入与执行回执、画面表现及推进；涉及停滞、折返或画面问题时，必须说明当时信息并查看对应截图。
- 三级测试默认由引擎自身的侦探替代玩家输入（使用主体认知、动机与共同决策机制在正式游戏循环中执行），测试 Agent 负责独立审查；只有用户明确要求盲玩时才采用独立盲玩。

## 宿主与核心合同

1. **分层隔离原则**：
   - UI 控件严禁直接访问 Core 对象引用；Core 严禁依赖任何 Godot 场景或节点。
   - `Host/Main.tscn` 与 `Host/GameEngine.cs` 是唯一胶水，负责双向数据转发：将 Core 演算结果与回执转为纯数据派发给 UI 和现场，将玩家/坐席操作转为 Core 意图交由世界引擎裁决。

2. **主体共同决策与适配**：
   - 正式主体（玩家席位与 NPC）统一通过 Core `SubjectiveAgent` 共享决策政策。
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
- `dotnet run --project godot_csharp/tests/UnifiedHostCheck`：宿主生命周期机制排错。

## 六、严禁硬编码剧情

严禁将剧情内容、对白文案、角色台词或谜题答案写入引擎代码（无论是 C# 还是 GDScript）。所有具体剧情与设定查验必须通过剧本编译物（`.story` 产物）和本地化词汇表读取。
