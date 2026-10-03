# 安眠者有福

Blessed Are Those Who Sleep。当前主线是孤岛学园游戏，目标为可游玩、可实际绘制并可录制进宣传片的内容片段。

地图基础格为 **32×32 像素**，角色与大型物件支持跨格。开发、制作与完成判定统一遵循 [设计与验收规范](docs/design-standard.md)。

## 当前入口与状态

- [主控设计与验收规范（重制版）](docs/design-standard.md)：重制分支的唯一设计依据。[设计总纲](docs/design-standard.md#设计总纲)给出核心判断（信念重演为核心动词，真话之敌、记忆之价为另两根支柱）与删除测试；[第二部分](docs/design-standard.md#第二部分第一章门的这一侧)是第一章《门的这一侧》的真相、题面、12 件证物、重演变量、解空间、触物回忆与始业之答；[第三部分](docs/design-standard.md#第三部分系统与界面)是 Core 映射与界面；[第五部分](docs/design-standard.md#第五部分实施与验收)是实施与验收。
- `godot_csharp/project.godot`：Godot C# 游戏工程。默认 `scenes/Main2D.tscn` 加载[第一章《门的这一侧》](godot_csharp/scenes/Awakening/README.md)：生活翼现场调查、昨夜重演（跑在 Core 上的感知—认知—决策推演，墨影回放与四人心声气泡）、触物回忆与钩、始业之答（含反问）与退出考题。实现报告与自检截图在 `artifacts/remake-belief-replay/`；第一次独立盲玩（ch1-blind-01）的结论与第三轮修订见[设计第二部分 §9](docs/design-standard.md#9-第一次独立盲玩ch1-blind-01与第三轮修订)。
- 旧第一章《未归的红筹》场景保留在 `godot_csharp/scenes/FirstAwakening/`，不再是默认入口，也不再作为设计依据。
- [家具喝茶推理原型](godot_csharp/scenes/Reasoning/README.md)：公共研究室调查与家具喝茶重演。
- [宣传前三场人物动作图集](godot_csharp/ShareAsset/Internal/Characters/animation_v2/README.md)：每人 111 帧位，含 Godot 资源与各方向动作规范；在[剧情打样场景](godot_csharp/scenes/Story/README.md)中验证。
- [21人32×48正面站立帧](godot_csharp/ShareAsset/Internal/Characters/README-zh.md)：原生像素角色资源与日常携带规范。

## 构建与验证

使用 .NET 8 和 Godot 4.6.1 .NET：

```powershell
dotnet build csharp/Core.sln
dotnet build godot_csharp/DualOntologyWorldEngine.sln
dotnet run --project godot_csharp/scenes/Awakening/check/ReplayCheck          # 第一章：216 种组合穷举、感知与回忆隔离
& $env:GODOT --headless --path godot_csharp --script res://scenes/Awakening/check/flow_check.gd   # 第一章：流程跳转回归
```

第一章的检查入口与盲玩驱动见[场景说明](godot_csharp/scenes/Awakening/README.md)。旧入口：

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
