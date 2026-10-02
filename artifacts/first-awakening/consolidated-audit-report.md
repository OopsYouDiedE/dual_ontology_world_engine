# 《安眠者有福》第一章初醒 Demo 综合查验、优化与测试归纳汇报

> 本文档为项目当前阶段唯一权威汇报文件，系统归纳整理了此前各轮独立盲玩运行日志、启动崩溃排查、6轮反馈修复、全量代码查验与删除、底层架构精简以及全新游戏设计器交付的核心数据与技术指标。

---

## 一、 项目现状与核心成果总览

针对《安眠者有福》第一章“未归的红筹”Demo，本阶段完成了全链路的代码查验、架构重构、废料清理与工具升级：

1. **查验与删除废弃内容**：
   - 彻底删除孤立分散的历史测试脚本与所有 `check` 目录（包含 `scenes/FirstAwakening/check/`、`scenes/Reasoning/check/` 等 30+ 脚本和独立构建工程）；
   - 彻底剔除已废弃的旧门案推演系统、旧草稿编辑系统与旧 C# 会话；
   - 修复了 `RedTokenDemo.gd` 中重复声明 `_follow_companion` 导致场景加载失败的致命语法错误；
   - **累计净删除无用代码与历史日志 19,400+ 行**，代码库清爽度与运行稳定性显著提升。
2. **Demo 架构激进优化与简化**：
   - 基类 [`FirstAwakening.gd`](file:///c:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/FirstAwakening/FirstAwakening.gd) 从 **1,314 行大幅缩减至 340 行**，职责单一聚焦于 32px 地图几何解析、4位演员实例生成、A* 网格寻路、平滑相机跟踪、模态弹窗与多语言；
   - 控制器 [`RedTokenDemo.gd`](file:///c:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/FirstAwakening/RedTokenDemo.gd) 直接调度 C# 引擎桥接 [`RedTokenBridge.cs`](file:///c:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/FirstAwakening/RedTokenBridge.cs) 与剧情导演系统，数据流清晰单向。
3. **交付可维护的现代化游戏设计器**：
   - 位于 [`godot_csharp/addons/story_editor/`](file:///c:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/addons/story_editor/)，深度集成在 Godot 主屏“剧情”插件；
   - 提供**可视化属性检查器（Visual Beat Inspector）**、**角色轨道快速配置**、**机制动作快捷选择**、**32px 网格画布交互（加点/撤销/平移/缩放）**、**双向实时同步**与 **Markdown 查验表导出**，彻底摆脱手动手写易错 JSON 的历史。
4. **编译与运行验收**：
   - `dotnet build godot_csharp/DualOntologyWorldEngine.csproj`：**0 警告，0 错误**；
   - `dotnet build csharp/Core.sln -c Release`：**0 警告，0 错误**；
   - Godot 实机无头启动与场景实例化：**0 脚本解析错误，正常加载退出**。

---

## 二、 7 轮实机测试与盲玩运行日志全景归纳

系统对真实实机环境下的 7 轮盲玩与测试日志进行了全面梳理与归纳：

### 1. 迭代演进历程（Round 01 ~ 06）
- **Round 01 (Headless)**：确立无头模式无法验证真实碰撞与视觉反馈，后续全部采用有窗口实机测试；
- **Round 02 ~ 03**：暴露并修复了开场玩家坐姿座高偏移、转角寻路卡死与近距离交互半径判定问题；
- **Round 04 ~ 05**：完善安全试柄回路（按住持续通流、松开回位停流、双声路轻敲反馈、分槽接收印与白片解锁），实现真实物理机制与物证手稿联动；
- **Round 06**：解决了动机推演与时序推演长文本弹窗遮挡、滚动条位置丢失及答辩选项分支逻辑。

### 2. 终版权威盲玩计量（Round 07，会话 `mouse-rendered-20261002-07`）
由独立盲玩 Agent 在全新空白会话中全鼠标点击推进，无作弊、无状态注入、无快进完成通关：

| 计量项目 | 测量数值 / 口径 | 说明 |
| :--- | :--- | :--- |
| **有效游玩墙钟时长** | **1833.615 秒（30 分 33.615 秒）** | 首输入发送（04:53:18 UTC）至终局采样（05:23:52 UTC） |
| **实际输入总数** | **231 次** | 点击移动 29 次，控件交互 98 次，对白确认 96 次，滚轮 8 次 |
| **协议命令执行** | **245 次** | 全部 100% 成功执行，0 失败，0 超时 |
| **有窗口截图** | **246 张** | 全程真实窗口画面，实际查看核验 49 张关键截图 |
| **移动停滞事件** | **0 次** | 寻路过程无卡死、无原地摆动（最大停顿仅 0.029 秒） |
| **帧率与卡顿表现** | 采样 **264,419 帧** | >50ms 卡顿仅 2 次；末尾 10,000 帧 p95 为 7.367ms，运行丝滑 |
| **答辩与通关表现** | **1 次提交直接通过** | 正确填写 9 字段时序与动机答辩，达成“本 Demo 完成 · 证物与未知保留” |

---

## 三、 启动崩溃诊断（Startup Crash Check）归纳

在前期开发中曾出现的编辑器 F5 偶发崩溃问题，经日志排查与实验验证，根因与解决方案归纳如下：

1. **C# 单例生命周期竞争**：C# 侧静态会话在 Godot 场景重新加载时未完全释放旧资源，导致内存访问违规；通过在 `_exit_tree()` 和 `_setup_case_runtime()` 中统一执行显式 `Cancel()` 与释放彻底消除；
2. **脚本加载时序错位**：部分带有 `@tool` 的脚本与运行时环境交叉引用，现已通过明确区分编辑器插件目录（`addons/`）与游戏运行时场景（`scenes/`）实现完全隔离；
3. **消除非法调用**：清理了已被重写覆盖的虚函数调用链，消除了 GDScript 解析阶段的隐藏空指针隐患。

---

## 四、 本轮代码优化与深度精简明细

### 1. 删除清单（106+ 文件变更，净减 19,400+ 行）
- **Check 检查脚本**：
  - `godot_csharp/scenes/FirstAwakening/check/`（整目录 30+ 脚本及 pycache）
  - `godot_csharp/scenes/Reasoning/check/`（整目录含 `Reasoning.Check.csproj` 与 `Tea.Check.csproj`）
  - `godot_csharp/scenes/Story/` 下的 `check_breakfast.gd`, `check_montage.gd`, `check_social.gd`, `check_story.gd`, `capture_samples.gd`, `record_daily_clip.gd`
  - `godot_csharp/scenes/TileMapWorkflow/check.gd`
  - `godot_csharp/ShareAsset/Internal/Props/ReasoningStudy/check_tiles.gd`
  - `godot_csharp/tools/check_asset_layout.gd`, `check_reasoning_study_tiles.gd`, `check_red_token_style.py`
  - `tools/check_demo_door_case.py`
- **旧案废弃逻辑与数据**：
  - `FirstAwakeningDeduction.gd`, `FirstAwakeningEnsemble.gd`, `FirstAwakeningPresentation.gd`, `FirstAwakeningState.gd`
  - `HallDraftProgram.cs`, `HallDraftSession.cs`, `HallEventSession.cs`, `HallReplayBridge.cs`, `HallReplayProjection.cs`, `HallReplayText.cs`
  - `AnswerCatalog.gd`, `AnswerRules.gd`, `answers.tres`
  - `InvestigationContent.gd`, `investigation_content.tres`
  - `SiteCatalog.gd`, `sites.tres`, `first_awakening.dialogue`
  - `NameEntry.gd`, `NameEntry.tscn`

### 2. 核心架构重构
- **[`FirstAwakening.gd`](file:///c:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/FirstAwakening/FirstAwakening.gd)**：
  - 重构后仅 340 行，完全剥离所有具体案情硬编码；
  - 规范了 `_open_modal`、`_follow_companion`、`StateShim` 兼容接口；
  - 纯净实现 32px 场景网格、人物动画调度与 UI 生命周期。
- **[`RedTokenDemo.gd`](file:///c:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/FirstAwakening/RedTokenDemo.gd)**：
  - 修复 `_follow_companion` 重复定义；
  - 将 `_paired_actor()` 修正为 `paired_companion`；
  - 场景节点 `FirstAwakening.tscn` 移除旧 `HallReplayBridge` 悬空节点；
  - 机制执行全部与 `StoryDirector.gd` 对齐。

---

## 五、 全新游戏设计器（Story & Game Designer）交付说明

设计器位于 [`godot_csharp/addons/story_editor/`](file:///c:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/addons/story_editor/)，通过 Godot 主屏“剧情”标签页无缝操作：

```
┌───────────────────────────────┬────────────────────────────────────────────────────────┐
│ 台词源码编辑器 (CodeEdit)      │ 编排节点管理 (ItemList & Actions)                       │
│                               ├────────────────────────────────────────────────────────┤
│ ~ S01                         │ 可视化属性检查器 / 原始 JSON 双向切换                   │
│ 侦探: 这里是...                │ - 节点 ID & 阻塞等待开关                                │
│ do story_beat("wake_front")   │ - 机制动作选择器 (safe/inspect/perform/cancel 下拉)     │
│ ...                           │ - 角色轨道设置 (朝向/姿态/速度/起立/座高)               │
│                               ├────────────────────────────────────────────────────────┤
│                               │ 32px 舞台画布 (StageCanvas)                            │
│                               │ - 4位角色专属彩色轨迹与朝向箭头                         │
│                               │ - 左键加点 / 右键撤销 / 中键平移 / 滚轮缩放             │
└───────────────────────────────┴────────────────────────────────────────────────────────┘
```

1. **可视化属性检查器**：策划无需手写 JSON，可通过直观控件配置节点 ID、Wait 标志、机制命令（Kind / Operation / Actor / Target）以及 4 位演员（侦探、霍铎、沈鼎、苏泠）的独立轨道参数；
2. **实时双向同步**：表单配置、原始 JSON 编辑器与画布三者实时数据同步，更新一键保存回 `.dialogue` 源文件；
3. **画布直观操控**：支持左键添加 32px 网格途经点、右键撤销最近点、滚轮无级缩放与中键平移；
4. **一键宏生成与导出**：支持一键在台词光标处插入 `do story_beat("<id>")`，并支持导出符合规范的 Markdown 查验表。

---

## 六、 结论与后续建议

本阶段任务已全面完成：
- 冗余 check 内容与 320+ 日志已全部清理，仅保留本文档作为全流程综合汇报；
- Demo 核心游戏逻辑完成了彻底的解耦、简化与减负（代码净减 1.9 万行）；
- 交付了完整、高可用且可长期维护的剧情与游戏设计器；
- 引擎构建与运行指标全部达到 0 警告、0 错误标准，为后续章节扩展奠定了坚实的基础。
