# 安眠者有福

Blessed Are Those Who Sleep。当前主线是孤岛学园游戏，目标为可游玩、可实际绘制并可录制进宣传片的内容片段。

地图基础格为 **32×32 像素**，角色与大型物件支持跨格。开发、制作与完成判定统一遵循 [设计与验收规范](docs/design-standard.md)。

## 当前入口与状态

- [主控设计与验收规范（重制版）](docs/design-standard.md)：重制分支的唯一设计依据。[设计总纲](docs/design-standard.md#设计总纲)给出核心判断（信念重演为核心动词，真话之敌、记忆之价为另两根支柱）与删除测试；[第二部分](docs/design-standard.md#第二部分第一章门的这一侧)是第一章《门的这一侧》的真相、题面、12 件证物、重演变量、解空间、触物回忆与始业之答；[第三部分](docs/design-standard.md#第三部分系统与界面)是 Core 映射与界面；[第五部分](docs/design-standard.md#第五部分实施与验收)是实施与验收。
- `godot_csharp/project.godot`：Godot C# 游戏工程。默认 `scenes/Title/Title.tscn` 选章，第一章经 `scenes/Main2D.tscn` 加载[《门的这一侧》](godot_csharp/scenes/Awakening/README.md)：生活翼现场调查、昨夜重演（跑在 Core 上的感知—认知—决策推演，墨影回放与四人心声气泡）、触物回忆与钩、始业之答与退出考题。实现报告与自检截图在 `artifacts/remake-belief-replay/`；第一次独立盲玩（ch1-blind-01）的结论与第三轮修订见[设计第二部分 §9](docs/design-standard.md#9-第一次独立盲玩ch1-blind-01与第三轮修订)。
- 旧第一章《未归的红筹》已退出运行目录；仍在使用的语言设置与翻译资源原样迁至 `godot_csharp/scenes/Shared/Locale/`。
- 第二章入口为 `godot_csharp/scenes/Chapter2/Game/Chapter2Game.tscn`，须从第一章的真实会话继续。正式作者源为 `godot_csharp/scenes/Chapter2/Script/chapter_02.world`，生成 `generated_chapter_02.json`；C# `Game` 保留人物自主行动与案件推演，`Chapter2InteractionSession` 负责场景事件、候选与剧情推进，Godot 负责几何、输入和呈现。迁移后的实机验证交由 Gemini，尚待结果。
- [宣传前三场人物动作图集](godot_csharp/ShareAsset/Internal/Characters/animation_v2/README.md)：每人 111 帧位，含 Godot 资源与各方向动作规范；在[剧情打样场景](godot_csharp/scenes/Story/README.md)中验证。
- [21人32×48正面站立帧](godot_csharp/ShareAsset/Internal/Characters/README-zh.md)：原生像素角色资源与日常携带规范。
- 正式第一、二章场内动作使用 `godot_csharp/ShareAsset/Internal/Characters/AnimeCast/` 的半 Q 人物：放大头部与上身、缩短腿部，由 `godot_csharp/tools/build_anime_cast.py` 注册。台词左侧另用 `DialogueCast/` 的正常比例上半身立绘，六种情绪配合姿势，由 `godot_csharp/tools/build_dialogue_cast.py` 注册。两套高分辨率透明源图及提示词分别归 `godot_csharp/AssetsSource/Internal/Characters/` 下的同名目录。地图、脚底坐标与碰撞尺度保持原有逻辑。

## 构建与验证

UI 的编辑器预览入口为 `godot_csharp/scenes/Shared/UI/UIWorkbench.tscn`。选择根节点，在检视栏的 `Preview Page` 切换界面；各页引用正式使用的预制件。进入原预制件后，静态文案、布局、纹理与主题直接编辑对应子节点；根脚本的预览字段用于显示对白选项、手稿变量等动态内容。UI 脚本使用 GDScript `@tool`，预览不启动剧情或播放音效。共享样式保存在 `scenes/Shared/UI/PaperTheme.tres`；两章专属页位于各章的 `UI/`。

使用 .NET 8 和 Godot 4.6.1 .NET：

```powershell
dotnet build csharp/Core.sln
dotnet build godot_csharp/DualOntologyWorldEngine.sln
dotnet run --project godot_csharp/scenes/Awakening/check/ReplayCheck          # 第一章：216 种组合穷举、感知与回忆隔离
& $env:GODOT --headless --path godot_csharp --script res://scenes/Awakening/check/flow_check.gd   # 第一章：流程跳转回归
```

第一章的检查入口与盲玩驱动见[场景说明](godot_csharp/scenes/Awakening/README.md)。

两章都使用独立 Core 与无 Godot 依赖的章节运行时，作者入口如下：

- 第一章：[ch1_awakening.py](godot_csharp/scenes/Awakening/Script/ch1_awakening.py) 是对白、分支、证物内容和现场热点的生产源；[Chapter1Script.cs](godot_csharp/scenes/Awakening/Script/Chapter1Script.cs) 保留调查流程、世界初始化与 `Night/` 推演。编译器生成纯 C# 对白数据、内容 JSON 和宿主布局 JSON，由同目录工程编入正式程序集。
- 第二章：[chapter_02.world](godot_csharp/scenes/Chapter2/Script/chapter_02.world) 声明房间、人物画像、物件、交互点、扁平候选与剧情台词；[Chapter2Script.cs](godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs) 及 `Prototype/` 保留 Core 规则、人物策略、行动、重演和结局算法。

两个章节工程仍编译为 `Chapter1Script.dll`、`Chapter2Script.dll`。第二章作者文件先经 AST 编译为 JSON，运行时从该文件读取声明并绑定真实 Core 对象；对白框仅消费 C# 发出的语义事件。

编辑第一章 Python 源后生成内容；`--check` 只检查生成物是否与源一致：

```powershell
python godot_csharp/tools/compile_story.py
python godot_csharp/tools/compile_story.py --check
```

世界脚本前端位于 `godot_csharp/tools/world_script.py`，使用 Python 风格声明加三反引号对白的 `.world` 文件；它只解析 AST，不执行作者代码。第二章正式文件位于 `scenes/Chapter2/Script/chapter_02.world`，示例仍位于 `scenes/Shared/Story/Examples/WorldScript/`。玩家选项与 NPC 反应平铺声明；Core 检查可用性并为 NPC 选择适用反应。第二章借用第一章运行中的 Core 会话及其人物实体和认知，不从存档重建。

```powershell
python godot_csharp/tools/world_script.py godot_csharp/scenes/Shared/Story/Examples/WorldScript/chapter_01_arrival.world godot_csharp/scenes/Shared/Story/Examples/WorldScript/chapter_02_morning.world -o godot_csharp/scenes/Shared/Story/Examples/WorldScript/generated_world.json
python godot_csharp/tools/world_script.py godot_csharp/scenes/Chapter2/Script/chapter_02.world --external-chapter awakening -o godot_csharp/scenes/Chapter2/Script/generated_chapter_02.json
```

构建 Godot 工程后，可从编辑器运行 `res://scenes/Shared/Story/Examples/WorldScript/Host/WorldScriptPreview.tscn` 查看作者预览；正式第二章请从标题页进入第一章，再经章节结束进入第二章。跨进程存档恢复和第一章统一 `.world` 迁移尚未完成。

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

第二章地图宿主通过 `WorldScriptPath` 或 `--world-script=路径` 加载扩展。`ExampleWorld.cs` 只演示该现役接口，不充当两章正式剧本。第一章已移除无人调用的旧 DLL 扩展桥；其正式入口使用编译后的章节交互数据。

两章共用延续中的 `WorldRuntime`、`SimulationSession` 及已存在的人物实体与认知；各章的脚本注册表绑定到这些真实引用。逻辑房间声明不包含 Godot 地图几何；职业行动仍由第二章 C# `Game` 执行。

两章使用共同对白、纸页与现场表现组件；本轮采用远端 `scenes/Shared/ReplayManuscript.gd` 与 `scenes/Shared/SightMask.gd`。共同键位为点击／WASD 走动、E 检视交谈、Tab 笔记、R 重演、H 回看、Esc 合页；第二章增加 M 学园图与 T 过一阵。调查与重演不耗时，改变他人认知的行动结算一个时段。未到访房间内部完全遮蔽，所有墙壁与门保持显示；实际进入室内地面后记录到访，无须检视，门与墙不计入进入范围。室内可见性按房间判断：当前房间整体可见，其他已到访房间只压暗内部，墙与门保持正常显示；宿舍与其他房间共用遮蔽逻辑，不需要迷雾的场景可直接展示环境。室内其他房间的人物与热点不显示；通过门洞不切换当前房间。第二章不再通过 `ConsoleUI` 桥驱动游戏界面；命令行入口独立保留为开发工具。

第二章独立盲玩使用真实命令行，先构建 `godot_csharp/scenes/Chapter2/Prototype/Prototype.csproj`。下列 `start` 须保持运行，在另一终端使用 `snapshot` 与 `send`；每个新盲玩者使用新的会话名。完整输入与输出保存于 `artifacts/remake-chapter2/<会话名>/`。画面验收另用 Godot 脚本截图，不作为盲玩。

```powershell
python godot_csharp/tools/chapter2_cli.py --session my-chapter2-play start
python godot_csharp/tools/chapter2_cli.py --session my-chapter2-play snapshot
python godot_csharp/tools/chapter2_cli.py --session my-chapter2-play send "帮助"
python godot_csharp/tools/chapter2_cli.py --session my-chapter2-play stop
```

统一自动回归入口：

```powershell
python tools/verify.py
```

`tools/verify.py` 依次执行 Core 构建、Core 作用域回归、宿主构建、第一章 `Awakening/check/flow_check.gd`、第二章 `Chapter2/Game/check_flow.gd` 与第一章 `state_gallery.gd`（截图输出到 `artifacts/remake-belief-replay/gallery/`）。可用 `--only flow flow2` 仅执行两章流程，或 `--skip` 跳过步骤；`--godot` 或环境变量 `GODOT` 指定 Godot .NET 控制台程序。以上为自动回归与截图检查，不替代独立盲玩。

素材迁移或重建后，用 Godot .NET 先执行 `--headless --editor --path godot_csharp --import`，再执行 `--headless --path godot_csharp --script res://tools/check_asset_layout.gd` 检查运行时依赖和资源加载。实际交互回归入口见各场景说明。

Core 使用 Essence–Projection 模型及嵌套行动值调度，独立于 Godot。房间内空间、碰撞、显示与交互由游戏宿主层负责。维护约束见 [csharp/AGENTS.md](csharp/AGENTS.md) 与 [godot_csharp/AGENTS.md](godot_csharp/AGENTS.md)。

## 架构与文件归属

剧情新增与修复以当前《安眠者有福》的正式章节为准。第一章编辑 `ch1_awakening.py` 并生成内容；第二章编辑 `chapter_02.world` 并生成 JSON。旧红筹案与 `.dialogue` 编辑路径不再作为正式运行或验收入口；第二章迁移须由 Gemini 完成构建、流程和实机验证后才可认定通过。

`csharp/` 为独立世界引擎，构建方式见 [Core README](csharp/README.md)。`godot_csharp/` 为游戏宿主，通过 `ProjectReference` 单向引用 Core；房内几何、碰撞、实时表现、UI 与具体游戏内容留在宿主。

游戏美术生产源统一归 `godot_csharp/AssetsSource/`，游戏直接调用的运行时资产统一归 `godot_csharp/ShareAsset/`。两者均分为 `External/<SourcePackage>/` 和 `Internal/{Characters,Identity,Props}/`。运行时资产由生产链路规范复制到 `ShareAsset/`。

`docs/` 唯一维护主控设计规范 `docs/design-standard.md`；`music/` 保存音乐工程与原声；`artifacts/<task-name>/` 保存阶段性成果与验证交付物。通用工具归 `tools/`，游戏资产生产与 Godot 工具归 `godot_csharp/tools/`。

## 素材入口

[Godot素材插件](godot_csharp/addons/rpg_asset_adapter/README.md) 提供统一转换入口。外部素材按原生尺寸读取，切片参数严格依据原生素材规格配置。概念原案用于指导设计，实机表现依运行时资产验收。
