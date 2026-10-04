# 安眠者有福

Blessed Are Those Who Sleep。当前主线是孤岛学园游戏，目标为可游玩、可实际绘制并可录制进宣传片的内容片段。

地图基础格为 **32×32 像素**，角色与大型物件支持跨格。开发、制作与完成判定统一遵循 [设计与验收规范](docs/design-standard.md)。

## 当前入口与状态

- [主控设计与验收规范（重制版）](docs/design-standard.md)：重制分支的唯一设计依据。[设计总纲](docs/design-standard.md#设计总纲)给出核心判断（信念重演为核心动词，真话之敌、记忆之价为另两根支柱）与删除测试；[第二部分](docs/design-standard.md#第二部分第一章门的这一侧)是第一章《门的这一侧》的真相、题面、12 件证物、重演变量、解空间、触物回忆与始业之答；[第三部分](docs/design-standard.md#第三部分系统与界面)是 Core 映射与界面；[第五部分](docs/design-standard.md#第五部分实施与验收)是实施与验收。
- `godot_csharp/project.godot`：Godot C# 游戏工程。默认 `scenes/Main2D.tscn` 加载[第一章《门的这一侧》](godot_csharp/scenes/Awakening/README.md)：生活翼现场调查、昨夜重演（跑在 Core 上的感知—认知—决策推演，墨影回放与四人心声气泡）、触物回忆与钩、始业之答（含反问）与退出考题。实现报告与自检截图在 `artifacts/remake-belief-replay/`；第一次独立盲玩（ch1-blind-01）的结论与第三轮修订见[设计第二部分 §9](docs/design-standard.md#9-第一次独立盲玩ch1-blind-01与第三轮修订)。
- 旧第一章《未归的红筹》场景保留在 `godot_csharp/scenes/FirstAwakening/`，不再是默认入口，也不再作为设计依据。
- 第二章入口为 `godot_csharp/scenes/Chapter2/Game/Chapter2Game.tscn`；完整复用第一章对白、纸页、高亮、寻路与重演界面，剧情直接取自同一份 C# `ConsoleUI`。本轮实现范围与双测试结果见[第二章重构报告](artifacts/remake-chapter2/refactor-v5/report.md)。
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

第一章的检查入口与盲玩驱动见[场景说明](godot_csharp/scenes/Awakening/README.md)。

两章默认使用各自正式的纯 C# 剧本，复用同一个 `scenes/Shared/Story/ChapterRuntime.csproj`：

- 第一章：[Chapter1Script.cs](godot_csharp/scenes/Awakening/Script/Chapter1Script.cs)。同目录工程编入原调查剧情、世界初始化、全部 `Night/` 推演源码，并嵌入原 `AwakeningContent.json` 和 `awakening.dialogue`。
- 第二章：[Chapter2Script.cs](godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs)。同目录工程编入原人物与世界初始化、开场文本及其余 `Prototype/` 中的完整规则、行动、时段剧情、对质和结局源码；`Program.cs` 保留为命令行入口。

正式剧本的源码由同名 `.csproj` 列明，整章编译为 `Chapter1Script.dll`、`Chapter2Script.dll`。编译工具接受入口 `.cs` 或工程 `.csproj`；游戏工程构建自动编译并引用两份正式剧本，默认启动直接使用这些程序集。第一章对白由原 DialogueManager 解析器读取剧本程序集内的原文，GD 不再另读磁盘对白或调查内容。

```powershell
./godot_csharp/tools/compile_world_script.ps1 -Source godot_csharp/scenes/Awakening/Script/Chapter1Script.cs -Output tmp/chapter-scripts/chapter1
./godot_csharp/tools/compile_world_script.ps1 -Source godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs -Output tmp/chapter-scripts/chapter2
dotnet build godot_csharp/DualOntologyWorldEngine.sln
```

正式剧本的世界与剧情类型实际定义在上述 DLL 中；它们与下面的外置扩展示例有不同编译归属，不能把正式 DLL 当作 `--world-script` 扩展插件传入。修改正式剧本后重新构建游戏即可更新默认章节。普通 C# 扩展剧本的编译与第二章命令行加载仍为：

```powershell
./godot_csharp/tools/compile_world_script.ps1 -Source godot_csharp/scenes/Shared/Story/Examples/ExampleWorld.cs -Output tmp/world-script
dotnet run --project godot_csharp/scenes/Chapter2/Prototype -- play --room-scoped --world-script tmp/world-script/WorldScript.ExampleWorld.dll
```

第一章扩展示例为 `scenes/Shared/Story/Examples/ExampleAwakening.cs`；同一工具编译后由 `Awakening.run_script(绝对DLL路径)` 加载。第二章地图宿主通过 `WorldScriptPath` 或 `--world-script=路径` 加载扩展。示例只演示接口，不充当两章正式剧本。脚本选择、等待和人物动作由同一运行器推进；原命令行默认入口保持原来的可用命令。

两章的 `ScriptWorld` 使用同一个 `CoreScriptWorld`，可直接定义新的 Core 人物、逻辑空间和物件，并复用原观察、规则、动作与自主决策。逻辑空间的声明不包含地图几何；原章的地图和职业系统继续使用原内容。

两章实际界面共同使用 `scenes/Shared/UI/` 的原对白场景、现场 HUD、Paper 样式、纸页与页脚、证物图文布局和重演双页组件。共同键位为 E／点击交互、Tab 笔记、R 重演、Q 行动、H 回看、Esc 合页。第二章 Q 只呈现现场动作；休息与睡下通过真实椅子与床交互，守夜在当前房间进行。游戏不提供命令输入或菜单瞬移，原命令行入口独立保留。Tab 按地点和记录类别列出侦探已知的卡片；调查卡保存原 C# 调查发生时的描述及时间地点来源，不用世界当前真值更新旧记录。R 按原合法候选的人物、规矩或位置组织具体问题，并保留原重演编号、最多三项和异步结算。没有合法候选时显示已见痕迹与待查地点。第二章的离散时段规则保持原样。

第二章独立盲玩使用真实命令行，先构建 `godot_csharp/scenes/Chapter2/Prototype/Prototype.csproj`。下列 `start` 须保持运行，在另一终端使用 `snapshot` 与 `send`；每个新盲玩者使用新的会话名。完整输入与输出保存于 `artifacts/remake-chapter2/<会话名>/`。画面验收另用 Godot 脚本截图，不作为盲玩。

```powershell
python godot_csharp/tools/chapter2_cli.py --session my-chapter2-play start
python godot_csharp/tools/chapter2_cli.py --session my-chapter2-play snapshot
python godot_csharp/tools/chapter2_cli.py --session my-chapter2-play send "帮助"
python godot_csharp/tools/chapter2_cli.py --session my-chapter2-play stop
```

旧入口：

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
