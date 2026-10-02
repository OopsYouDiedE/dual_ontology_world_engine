# 剧情模块化与游戏设计器交付与验证综合报告

> 本文档为 `artifacts/story-modularization/` 唯一权威报告，系统归纳整理了剧情模块化重构、C# 与 GDScript 契约解耦、可视化游戏设计器（Story Editor）实现与自动化回归验证结果。

---

## 一、 交付背景与模块化目标

为解决此前剧情对白与底层案件状态高度耦合、硬编码走位及缺乏可视化编辑工具的问题，实施了剧情模块化重构：

1. **机制与对白解耦**：将对话脚本（`red_token.dialogue`）与底层 C# 世界状态通过标准节拍机制（`story_beat`）解耦；
2. **导演系统引入**：引入 [`StoryDirector.gd`](file:///c:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Story/StoryDirector.gd) 与 [`StoryScore.gd`](file:///c:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Story/StoryScore.gd)，统一管理角色动画轨道、A* 寻路调度、并发行走与到达汇合点同步；
3. **交付可视化游戏设计器**：在 Godot 编辑器内提供原生主屏“剧情”插件（[`godot_csharp/addons/story_editor/`](file:///c:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/addons/story_editor/)），让策划与剧作人员通过可视化 UI 和 32px 画布直接调度舞台。

---

## 二、 游戏设计器（Story Editor）核心能力

- **集成形态**：Godot 编辑器顶部主屏插件，双击打开 dialogue 文件即可实时编辑；
- **核心功能**：
  1. **可视化属性检查器 (Visual Beat Inspector)**：表单化配置节点 ID、Wait 标志、机制命令（`safe` / `inspect` / `perform` / `cancel` 下拉）及关联参数；
  2. **角色轨道配置**：针对 4 位角色（侦探、霍铎、沈鼎、苏泠）独立配置朝向（`front`/`back`/`left`/`right`）、姿态（`idle`/`walk`/`sit`/`talk` 等）、行走速度与座高；
  3. **32px 网格画布交互**：角色专属色彩轨迹渲染，左键点击添加途经点、右键撤销最近点、滚轮无级缩放、鼠标中键拖拽平移；
  4. **双向实时同步**：可视化属性表单、32px 画布与底层原始 JSON 实时双向同步，并支持在台词光标处一键插入 `do story_beat("<id>")`；
  5. **静态格式校验与导出**：一键导出 Markdown 格式的完整节拍查验表，便于剧情审查与团队协作。

---

## 三、 首批节拍节点验证（以第一章主线为例）

| 节拍 ID | 台词锚点 | 角色动作 / 调度行为 | 机制提交 |
| :--- | :--- | :--- | :--- |
| `wake_left` | S01 | 侦探原位坐姿向左，座高 4px | 无机制提交，姿态设置完成后继续对白 |
| `wake_front` | S01 | 侦探原位坐姿向前，座高 4px | 无机制提交，姿态设置完成后继续对白 |
| `exit_party` | S28 | 四位演员同时起立，分别沿 A* 网格走至门扇前汇合排布 | 边走边说，汇合到达后触发退场机制与结局展示 |
| `safe_press` | E03 | 工程师走至试柄台 (424, 748)，面向后方保持按住动作 | 提交独立安全试件 press 动作，触发管路通流 |
| `safe_release`| E03 | 工程师原位释放试柄，回到待机状态 | 提交独立安全试件 release 动作，触发回位与切流 |

---

## 四、 技术回归与构建结果

- **Core 构建**：`dotnet build csharp/Core.sln -c Release` $\to$ **0 警告，0 错误**；
- **Godot 构建**：`dotnet build godot_csharp/DualOntologyWorldEngine.csproj` $\to$ **0 警告，0 错误**；
- **Core ScopeRegression**：21/21 项回归全部通过；
- **开场调查回归**：40/40 项输入与物证采集检查通过；
- **详细实施度量**：参见本目录 [`implementation-audit.json`](implementation-audit.json)。
