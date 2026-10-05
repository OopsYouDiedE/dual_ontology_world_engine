# 安眠者有福

Blessed Are Those Who Sleep。当前主线是孤岛学园游戏，目标为可游玩、可实际绘制并可录制进宣传片的内容片段。

地图基础格为 **32×32 像素**，角色与大型物件支持跨格。开发、制作与完成判定统一遵循 [设计与验收规范](docs/design-standard.md)。

## 当前入口与状态

- [主控设计与验收规范](docs/design-standard.md)：统合学园世界观设定、四大历史事故与羁绊物证、初期篇二十日节奏、初醒案情因果链、双重本体引擎原理、现场勘查 24 动作、32px 美术与 MCP 工作流，以及机制主线宣传片大纲。
- [设计总纲](docs/design-standard.md#设计总纲2026-10-02-整理)：设计支柱、案件／日程／长线三层循环、长线状态量、实现状态与待裁定事项。
- [学园世界与角色设定](docs/design-standard.md#第一部分学园世界与角色设定)：世界观边界、导师遗存、恶魔契约法定规则、胜负退场机制与 16 位角色属性包。
- [剧情主线与初醒案情](docs/design-standard.md#第二部分剧情主线与初醒案情)：叙事层级划分、四大历史事故物证链、生活翼空间拓扑、四人固定本能与闭合因果链。
- [Core 世界引擎原理](docs/design-standard.md#第三部分core-独立世界引擎运作原理)：Essence–Projection 双重本体模型、`Observation.Observe` 认知更新与 CTB 嵌套行动值调度。
- [推理动作与手稿规范](docs/design-standard.md#第四部分侦探推理与角色动作系统)：现场勘查 24 项因果桥梁动作表、角色 9 项基础动作与手稿界面视觉规范。
- [32px 美术与 MCP 工作流](docs/design-standard.md#第五部分32px-像素美术与地图工作流)：32px 地图格、32×48 角色帧预算、47-autotile 布局标准与资源分流法定路径。
- [首支宣传片主线大纲](docs/design-standard.md#第六部分宣传视频与成果验收规范)：75–80 秒分镜结构（日常、推演、言灵、真名救场、决战一瞥）与实机运行要求。
- `godot_csharp/project.godot`：Godot C# 游戏工程。默认 `scenes/Main2D.tscn` 加载[初醒 Demo](godot_csharp/scenes/FirstAwakening/README.md)：当前默认已接入《未归的红筹》的场景、调查UI与新道具。
- [第一章“未归的红筹”重设](docs/design-standard.md#4-初醒案件现场与因果链)：新案场景与UI已接入默认 Demo。已修复候选反馈、长页操作、鼠标改点与转角、规则参照隐式改写、初识与认签顺序等问题；最新全新有窗口鼠标盲玩正常完成，首输入至终局30分33.615秒，231输入、246PNG，无求助或运行卡关。[综合汇报](artifacts/first-awakening/consolidated-audit-report.md)归纳各轮评价与计量（原始轨迹目录已精简删除）；最后修复定向425项通过。真人净30分钟及发布体验仍未认证，旧案历史和其他轮次不替代本版结果。
- [家具喝茶推理原型](godot_csharp/scenes/Reasoning/README.md)：公共研究室调查与家具喝茶重演。
- [宣传前三场人物动作图集](godot_csharp/ShareAsset/Internal/Characters/animation_v2/README.md)：每人 111 帧位，含 Godot 资源与各方向动作规范；在[剧情打样场景](godot_csharp/scenes/Story/README.md)中验证。
- [21人32×48正面站立帧](godot_csharp/ShareAsset/Internal/Characters/README-zh.md)：原生像素角色资源与日常携带规范。

## 构建与验证

使用 .NET 8 和 Godot 4.6.1 .NET：

```powershell
python tools/verify.py
```

`tools/verify.py` 依次执行 Core 构建、Core 作用域回归、宿主构建、红筹跳转回归 `story_flow_check.gd` 与关键画面截图集 `state_gallery.gd`（输出到 `tmp/state_gallery/`），引擎日志出现 SCRIPT ERROR 即判失败；可用 `--skip` 跳过步骤，`--godot` 或环境变量 `GODOT` 指定 Godot .NET 控制台程序。以上均为注入状态的自动回归，不替代独立盲玩。

素材迁移或重建后，用 Godot .NET 先执行 `--headless --editor --path godot_csharp --import`，再执行 `--headless --path godot_csharp --script res://tools/check_asset_layout.gd` 检查运行时依赖和资源加载。实际交互回归入口见各场景说明。

Core 使用 Essence–Projection 模型及嵌套行动值调度，独立于 Godot。房间内空间、碰撞、显示与交互由游戏宿主层负责。维护约束见 [csharp/AGENTS.md](csharp/AGENTS.md) 与 [godot_csharp/AGENTS.md](godot_csharp/AGENTS.md)。

## 架构与文件归属

剧情新增与修复按[模块职责与任务顺序](docs/design-standard.md#415-剧情模块与新增任务边界2026-10-02)实施：当前红筹案已拆分内容、现场会话、候选历史与答辩表现，保留现有运行入口。

逐人物走位与机制编排使用[剧情编辑器](docs/design-standard.md#416-剧情编辑器与逐人物场面调度2026-10-02)：Godot顶部“剧情”主屏编辑同一份 `.dialogue` 中的台词与 `# @stage` 节点，导出逐人物 Markdown 查验表。首批接入开场转向、四人退场和安全试柄；本轮未构建或运行，尚未实机验收。

`csharp/` 为独立世界引擎，构建方式见 [Core README](csharp/README.md)。`godot_csharp/` 为游戏宿主，通过 `ProjectReference` 单向引用 Core；房内几何、碰撞、实时表现、UI 与具体游戏内容留在宿主。

游戏美术生产源统一归 `godot_csharp/AssetsSource/`，游戏直接调用的运行时资产统一归 `godot_csharp/ShareAsset/`。两者均分为 `External/<SourcePackage>/` 和 `Internal/{Characters,Identity,Props}/`。运行时资产由生产链路规范复制到 `ShareAsset/`。

`docs/` 唯一维护主控设计规范 `docs/design-standard.md`；`music/` 保存音乐工程与原声；`artifacts/<task-name>/` 保存阶段性成果与验证交付物。通用工具归 `tools/`，游戏资产生产与 Godot 工具归 `godot_csharp/tools/`。

## 素材入口

[Godot素材插件](godot_csharp/addons/rpg_asset_adapter/README.md) 提供统一转换入口。外部素材按原生尺寸读取，切片参数严格依据原生素材规格配置。概念原案用于指导设计，实机表现依运行时资产验收。

