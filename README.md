# Dual Ontology World Engine

DuelView（DV）世界与心智引擎，以及 Godot 游戏宿主。唯一正式动作链为 `Prepare → Execute → Complete → 呈现`。DV 管理权威状态、人物认识、决策、费用与观察；Godot 管理碰撞、输入、画面和现场执行回执。

## 工程结构

| 路径 | 职责 |
| --- | --- |
| `dv_core/compiler/` | DV 编译器、自举 Seed、Runtime |
| `dv_core/modules/20-core.dv`、`30-session.dv` | 通用机制、认知、规划与会话调度 |
| `dv_core/modules/35-campaign-schema.dv` | 人物目录、位置与时钟引用、公共构造器、跨章结构 |
| `dv_core/modules/40-chapter1.dv`、`50-chapter2.dv` | 各自依赖 35 的章节内容 |
| `dv_core/modules/60-game.dv` | 生产组合、连续章节与宿主投影 |
| `dv_core/base/`、`dv_core/host_bridge/` | 独立 Core 程序集、内容程序集、原生值桥与 StateDelta |
| `dv_core/cli/` | 无 Godot 依赖的文本、JSON-Lines、TUI 与智能回放 |
| `dv_core/checks/` | 独立逻辑运行器与 oracle，生产入口不引用 |
| `godot_csharp/` | 正式场景、运行脚本、Shader、契约与发布资源 |
| `mcp/` | 按需加载的构建、资产、音乐、编辑器、沙盒与诊断工具 |
| `docs/design-standard.md` | 世界、人物、剧情和玩法的设计规范 |
| `artifacts/` | 任务报告及原始证据 |

20/30 生成到 `DuelView.Core`，35–60 生成到 `DuelView.Session`。各模块独立解析并生成 C#，依赖通过函数和名义类型接口传递；模块正文变化只使自身生成缓存失效，接口变化影响消费者。输入不变时生成文件保持原时间戳。MCP/LSP 初始化启动共享编译器的后台预热。生成物位于各项目 `obj/`，原始资产位于 `mcp/asset_pipeline/sources/`。

Core 提供原生值会话和 CLI 两种宿主入口，二者使用同一 Prepare/Complete 实现。Core 只发布逻辑身份、位置、时间及观察者投影；Godot 的场景、角色图像、动画帧与 HUD 由宿主呈现目录映射。章节40/50分别依赖公共35，调试65和检查70保持在生产闭包之外。

## 构建和游玩

本轮六幕制作入口：`& $env:GODOT --path godot_csharp -- --highlights`，也可用 `--highlight=chess_god` 指定一幕。素材按自动地形、独立物件、人物分类，入口见 `godot_csharp/scenes/Highlights/README.md`；当前制作与实际观察放在 [HTML汇报](artifacts/dv-approx-highlights/index.html)。默认近似决策和多频模拟的实现范围见 [DV模拟接口](dv_core/SIMULATION.md)。

环境为 .NET 8 SDK、Godot 4.6.1 .NET 与 Python 3。在仓库根目录运行：

```powershell
dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers -p:NuGetAudit=false
dotnet build dv_core/LanguageCore.sln -c Release --disable-build-servers -p:NuGetAudit=false
& $env:GODOT --headless --editor --path godot_csharp --import
& $env:GODOT --path godot_csharp -- --dv-controller=player
```

`GODOT` 指向 Godot .NET 可执行文件；`-p:DvPython=<路径>` 可指定构建 Python。VS Code 默认打开游戏解决方案；Core、CLI 与编译器开发使用 `dv_core/LanguageCore.sln`。

游戏参数放在 `--` 后。`--dv-controller=player|ai` 选择控制者，`--dv-speed=1` 为正常速度，`--dv-auto-start` 直接开始。`--dv-audit` 选择完整来源审计；帧取证与阶段计时各自通过显式参数开启，默认关闭。宿主的取证与计时队列有容量上限。

同房间 WASD 或鼠标行走由真实碰撞执行，不消耗 DV 时间与行动次数。热点互动与过门使用当前本人候选和共同动作链。移动事务保留源地图、位置、朝向与执行房间；现场失败或 Complete 拒绝时恢复源侧表现。退出存档使用最近已提交状态，轨迹保留在途事件。

## 三条开发入口

### 地图和美术

```powershell
python mcp/godot_workspace.py --sandbox --godot $env:GODOT
python mcp/asset_pipeline/build_living_floor.py
python mcp/asset_pipeline/export_geometry.py
```

`MapSandbox` 使用纯 GDScript 和正式 StoryActor，直接读取场景碰撞、灯光声明和 `godot_csharp/Contracts/geometry_contract.json`。可切换地图、锚点、房间/门覆盖、灯光与遮罩。工具生成独立的临时工程，无需编译 C# 或 DV。修改房间分区与门连通时维护同一契约；导出器更新场景锚点并保留房间分区。

### UI 和现场交互

```powershell
python mcp/godot_workspace.py --mock-ui --godot $env:GODOT
& $env:GODOT --path godot_csharp -- --dv-dev --dv-controller=player
```

纯 GDScript MockSession 读取 `Contracts/presentation_mock_pack.json`，驱动正式翻页、对白、案卷、重演与现场界面；独立 UI 工作区无需 C# 或 DV 编译。开发会话按反引号或 F12 打开 CanvasLayer 128 控制台：命令、实时 Inspector、最近 120 帧环形性能记录和回放四个面板。Inspector 列出主客观状态、调度队列和按评分排列的候选；性能面板记录 FPS、Prepare、Complete 与寻路，关闭采样时停止写入。`act` 使用当前候选；`inspect` 查看指定主体；`tp`、`time +N` 记录开发操作并同步现场。

### Core 和文本控制

```powershell
dotnet run --project dv_core/cli/DvCli.csproj -- --tui
dotnet run --project dv_core/cli/DvCli.csproj -- --jsonl
dotnet run --project dv_core/cli/DvCli.csproj -- --replay <记录.dvdag> --speed 4
```

文本命令包含 `status`、`options`、`act <编号或ID>`、`next`、`save <路径>`、`trace <路径>` 与 `quit`。`--dev` 开启 `inspect`、`tp`、`time`。CLI 将执行器标为 `formal-model`，复用正式 Prepare/Complete；`--command` 执行失败退出 1。游戏控制台和 CLI 共用 SmartReplay：移动 0.3 秒、一般行动 0.6 秒、对白按字数 0.8–2 秒，忽略录制时的现实等待，支持单步及 1/2/4/8 倍速。Godot 在独立视口中使用实际 Main、WorldView、StoryActor 和 UI 场景呈现回放，消费日志中的输入、身体、页面和案卷状态。旧记录缺少的数据按实际记录范围展示。

## 工具和文档

`python mcp/server.py` 启动统一 stdio MCP 服务。`mcp/tools.json` 是公开操作与调用路由的唯一目录；列出工具时只读取静态数据，具体依赖在操作调用时加载。资产生成和发布、Godot 采集及输入工具、独立沙盒、编辑器和音乐子命令均由该目录提供入口。工具命令、工作区与资产生成说明见 [mcp/README.md](mcp/README.md)。音乐源与构建方式见 [音乐管线](mcp/music_pipeline/README.md)。

[维护约束](AGENTS.md)、[Core 说明](dv_core/README.md)、[语法参考](dv_core/SYNTAX.md)、[宿主接口](dv_core/host_bridge/README.md) 记录现役入口和语义。新轨迹使用 `.dvdag` / `.dvsave` / `.dvlog`；JSONL 仅用于文本进程通信。工程构建、形式逻辑、物理现场和实际画面各自保留对应证据。临时脚本放在 `.scratch/`，本轮任务结束时清理。
