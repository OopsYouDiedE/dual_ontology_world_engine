# 《安眠者有福》项目阶段成果与交付物总汇总 (Artifacts Index)

> 本目录为项目法定非运行时阶段成果、概念原案与独立验收报告的唯一归档归宿（依据 `AGENTS.md` 规范）。本轮已完成全面清理与瘦身归纳：**每个模块严格收拢至一份权威报告**，清理历史冗余截图、过期门案废料与字节码缓存。

---

## 一、 核心交付模块索引与权威报告

| 模块目录 | 核心主题 | 当前状态 | 唯一权威报告 / 核心交付物 | 文件精简度量 |
| :--- | :--- | :--- | :--- | :--- |
| **[`first-awakening/`](first-awakening/)** | 第一章主线《未归的红筹》Demo | **当前主线，已完成实机验证** | [`consolidated-audit-report.md`](first-awakening/consolidated-audit-report.md)<br>32px 场景物证、v2 视觉画面与推演原案图集 | 历史 1800+ 快照 $\to$ 35 项核心文件 (6.41 MB) |
| **[`story-modularization/`](story-modularization/)** | 剧情模块化与全功能游戏设计器 | **已交付运行** | [`report.md`](story-modularization/report.md)<br>`godot_csharp/addons/story_editor/`（主屏设计器） | 合并为单份总报告，保留实施审计 JSON (0.01 MB) |
| **[`island-3d-structure/`](island-3d-structure/)** | 学园孤岛 3D 地形与空间拓扑原案 | **概念设计归档** | [`validation.md`](island-3d-structure/validation.md)<br>`island_structure.blend` (68.9 MB)<br>全岛鸟瞰、分层、剖视与水源透视渲染图集 (00~07) | 清除 `__pycache__`，保留 21 项法定建模成果 (92.84 MB) |
| **[`code-optimization/`](code-optimization/)** | 核心世界引擎与代码度量基准 | **基线达标** | [`README.md`](code-optimization/README.md)<br>AST 复杂度分析、GC 分配降幅 80%+ 测量报告 | 移除旧门案截图与碎片报告，收拢为单份总报告 (0.02 MB) |
| **[`blipsmith-test/`](blipsmith-test/)** | 复古声效合成器与音频原型 | **原型归档** | [`report.md`](blipsmith-test/report.md)<br>`all-voices.zip`、`audio-validation.json` | 3 份报告合并为 1 份，清理重叠压缩包 (0.58 MB) |
| **[`deduction-balance/`](deduction-balance/)** | 假说因果链与推理平衡性审计 | **设计完成** | [`README.md`](deduction-balance/README.md)<br>因果链推演相容性与信息暴露边界法则 | 彻底清除已废弃的旧门案 JSON，纯化设计归档 (1.6 KB) |
| **[`asset-consolidation/`](asset-consolidation/)** | 32px 像素美术与资源分流规约 | **规范落地** | [`resource-validation.json`](asset-consolidation/resource-validation.json)<br>资产分流与原生切片校验执行记录 | 依赖加载校验通过，0 失败 |
| **[`releases/`](releases/)** | Core 独立双重本体世界引擎发行包 | **正式打包** | `DualOntologyWorldEngine.Core.1.0.0.nupkg` | 独立 C# 核心世界引擎发行包 (0.06 MB) |

---

## 二、 Artifacts 清理与归纳成果对比

经过两轮系统清理与深度归纳，`artifacts/` 目录实现了本质上的清爽度与可维护性提升：

```
【清理前】
  - 体积：~595 MB（充斥 1800+ 张历史盲测过程截图、重演中间帧、废弃测试快照）
  - 结构：各子目录存在多个零散、重复的阶段性汇报（如 7 份 first-awakening 独立子报告、3 份 blipsmith 报告、3 份 code-optimization 报告）
  - 废料：包含已退役旧门案的 `demo_door_case.json`、`paper-notebook` 截图、以及 Python `__pycache__` 缓存

【清理后】
  - 体积：~99.9 MB（其中 68.9 MB 为 Blender 3D 地形工程 `island_structure.blend`，24 MB 为 8 张 4K 岛屿概念渲染图）
  - 核心文档：每个模块严格收纳为 1 份权威总结报告，杜绝碎片化文档；
  - 关键资产：第一章《未归的红筹》全量保留 v2 终版实机视效图集与推演序列图，剔除 13 张失效的 v1 重复草样；
  - 纯净度：零临时缓存、零死代码产物、零重叠文档。
```

---

## 三、 各模块核心成果要点

### 1. 第一章主线核心成果 (`first-awakening/`)
- **唯一权威汇报**：[`artifacts/first-awakening/consolidated-audit-report.md`](first-awakening/consolidated-audit-report.md)
  - **7 轮实机盲测全景**：终版盲玩（Round 07）纯鼠标输入有效游玩时长 **30 分 33.615 秒**，231 次实际有效输入，0 错误，0 移动停滞，26.4 万采样帧仅 2 次卡顿；
  - **启动崩溃根治**：消除 C# 静态生命周期与 Godot 场景重载的内存并发竞态，修复 GDScript 语法冲突；
  - **代码与宿主精简**：删除 30+ 历史检查脚本、320+ 日志，宿主基类从 1314 行瘦身至 340 行；
  - **实机物证图集**：保留 16 张 v2 像素视效图、2 张 32px 道具原图、6 张宿主界面及 10 张三语因果推演全景图。

### 2. 剧情与游戏设计器 (`story-modularization/`)
- **唯一权威汇报**：[`artifacts/story-modularization/report.md`](story-modularization/report.md)
  - **核心组件**：Godot 编辑器顶部主屏“剧情”插件（`godot_csharp/addons/story_editor/`）与 `StoryDirector.gd`；
  - **核心能力**：
    - 可视化节拍检查器（Beat Inspector）：表单直观配置节点 ID、Wait 标志、机制命令（`safe` / `inspect` / `perform` / `cancel`）；
    - 4 位角色专属轨道配置：朝向、姿态、速度、座高；
    - 32px 画布增强：角色色彩轨迹、左键落点加路径、右键撤销、滚轮缩放、中键拖拽；
    - 双向实时同步：表单、画布与原始 JSON 实时同步，一键向台词插入 `do story_beat("<id>")`。

### 3. 世界引擎代码度量 (`code-optimization/`)
- **唯一权威汇报**：[`artifacts/code-optimization/README.md`](code-optimization/README.md)
  - **GC 内存优化**：AST 名词与引用收集重构，复杂输入同线程高频枚举内存分配减少 **61.37% ~ 83.88%**；
  - **会话状态隔离**：同 Node 桥接连续 25 轮 325 项调用检查全部通过，零跨调用状态污染；
  - **MCP 推演分层**：117 项动态基线对照 100% 通过。

### 4. 声音工具原型 (`blipsmith-test/`)
- **唯一权威汇报**：[`artifacts/blipsmith-test/report.md`](blipsmith-test/report.md)
  - **工具工作流**：9 项滑动参数与离散音色探索，支持中文/英文/emoji 逐字流式对白测试；
  - **PCM 底层度量**：14 个 WAV 样本 44.1kHz / 16bit / 单声道核验，零满幅硬削波，零直流跳变爆音。

### 5. 空间与学园拓扑 (`island-3d-structure/`)
- **唯一权威汇报**：[`artifacts/island-3d-structure/validation.md`](island-3d-structure/validation.md)
  - **原型与拓扑**：以 Faial 火山岛为原型，1,415m × 1,252m 范围，Faial 中央火口盆底（102.95m）与学园阶梯楼群（22.5m~27.0m）；
  - **工程资产**：完整的 Blender 3D 工程文件 `island_structure.blend` 及 8 张多视角概念渲染图。

---

## 四、 维护准则（依 `AGENTS.md`）

1. **唯一主控设计文档**：全系统机制、世界观与剧本统一维护于 [`docs/design-standard.md`](../docs/design-standard.md)，严禁自建零散 Markdown 描述机制；
2. **法定交付归宿**：`artifacts/<task-name>/` 持续复用既有模块目录，严禁在根目录或源码目录散落报告；
3. **独立盲玩准则**：新一轮试玩须由全新上下文 Agent 执行，不接收谜底与预写路径，真实记录输入日志与有效时长。
