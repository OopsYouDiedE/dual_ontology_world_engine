# 可复用工具中心

正式游戏资源和脚本位于 `godot_csharp/`；工具、原始资产及编辑器插件集中在本目录。所有命令从仓库根目录执行。

## 统一 MCP

```powershell
python mcp/server.py
```

服务通过 stdin/stdout 接收一行一个 JSON-RPC 消息。`tools.json` 同时声明公开操作与实际执行路由；`tools/list` 从这些路由生成枚举。调用时才导入所选函数或启动对应程序，执行器不再维护第二份操作名单。MCP 初始化调度一次后台 C3 编译器预热，请求线程不等待编译。其状态和日志由 `build/prewarm.py` 管理；`compiler_prewarm` 可显式等待缓存就绪。

| 工具 | 内容与参数 |
| --- | --- |
| `asset_pipeline` | 地图、角色、书桌、材质、UI、几何合同生成，以及三个 `publish-*` 原稿发布器；`operation` 与命令行 `arguments` |
| `music_pipeline` | `render`、`scores`、`rewrite-scores`、`from-mscz`、`export`、`audit`；默认 `render`，保留原 `arguments` 用法 |
| `compiler_prewarm` | 等待或建立共享编译器缓存，返回实际编译器路径 |
| `build_tools` | 显式调用 `session`、`language`、`host`、`cli` 构建；`session` 必须给 `--out` |
| `dv_cli` | Core 文本命令、批量输入和记录回放；`arguments`、可选 `stdin` |
| `map_workspace` | `mode=map` 打开纯地图沙盒，`mode=editor` 打开带插件的工具编辑器；可给 `godot` 路径 |
| `ui_mock` | 以纯 GDScript Mock 会话打开实际 Main/UI，自动加入 `--dv-mock` |
| `rpg_adapter` | 图像、绘图、地图导出、像素交接、工作记录、UI 度量；`arguments` 为所选函数的命名参数对象 |
| `diagnostics` | 现有 Python 逻辑记录、检查点、二进制读取、采集、性能、时序、编码、路径和几何工具 |
| `godot_tools` | 地图/UI/宿主采集、输入与物理观察、音乐作用域和 RPG 转换/渲染工具；目录声明 C#、渲染与工作区要求 |
| `adapter_checks` | 既有 `workflow`、`native-art`、`pixel-handoff`、`production`、`demo` 工程入口 |
| `editor_tools` | 共用编译诊断、实际 LSP 诊断发布、启动配置、VS Code 准备/依赖/打包、Visual Studio 构建/打包 |

函数调用采用 `{"operation":"inspect_image","arguments":{"path":"..."}}` 这样的参数结构；命令工具采用 `{"operation":"geometry","arguments":["--out","artifacts/<任务>/geometry.json"]}`。公开枚举以服务返回的 schema 为准。工具不在列举时执行检查、采集或会话。复用的库函数、场景组件和编译桥通过所属入口使用。

子进程的 stdout/stderr 返回给调用方；非零退出码在 MCP 响应中标记 `isError`。子进程不读取 MCP 的 stdin。交互工作区返回进程号与 `artifacts/tool-workspaces/<本次目录>/launch.log`。MCP stdout 始终用于协议。

## 目录

| 目录 | 内容 |
| --- | --- |
| `build/` | 自举编译器、后台预热、模块缓存与分区代码生成 |
| `asset_pipeline/` | 地图、角色、3D 书桌、材质、UI 和几何合同发布 |
| `asset_pipeline/sources/` | 可编辑原始资产；成品发布到 `godot_csharp/ShareAsset/` |
| `music_pipeline/` | 谱面、SF2 合成与音轨生成 |
| `rpg_adapter/` | 编辑器适配器及图像/像素/工作流工具 |
| `editor_lsp/` | DV 语言服务器、检查入口与 VS Code/VS/Godot 客户端 |
| `sandboxes/` | 纯 GDScript MapSandbox 和工具生成预览 |
| `verification/` | 二进制记录读取、采集、分析与独立检查 |

## Godot 工具工作区

```powershell
python mcp/godot_workspace.py --sandbox --godot $env:GODOT
python mcp/godot_workspace.py --mock-ui --godot $env:GODOT
python mcp/godot_workspace.py --editor --godot $env:GODOT
python mcp/godot_workspace.py --csharp --godot $env:GODOT --script res://tools/capture_title_3d.gd -- --out=<新目录>
```

工作区位于 `.scratch/`，链接正式场景、成品、契约和工具目录。纯地图与 Mock UI 工程移除 C# feature 和 dotnet 设置，不创建 csproj，也不调用 .NET 构建。Mock UI 保留 `Main.tscn` 为主场景，通过 `--dv-mock` 选择 GDScript 会话，使用独立临时用户数据。`--isolated-user-data` 可给其他工具采用同样的临时偏好和存档目录；输入探针的 MCP 路由自动启用该选项。

编辑器模式挂载两个工具插件。`--csharp` 才生成独立 `DuelView.Tools` 工程，并额外编入 `FrameArchive.cs`。各模式先导入其工作区资源，再打开场景或工具。CLI 退出时释放工程；长驻 MCP 中的资产导出/UI 度量使用 `workspace()` 作用域，调用结束即释放。工具生成的预览场景放在 `sandboxes/generated/`。

以下 `res://` 路径只属于临时工具工程，持续作为有效入口使用：

| 工具资源路径 | 仓库目录 |
| --- | --- |
| `res://tools` | `mcp/verification` |
| `res://asset_pipeline` | `mcp/asset_pipeline` |
| `res://AssetsSource` | `mcp/asset_pipeline/sources` |
| `res://sandboxes` | `mcp/sandboxes` |
| `res://addons/rpg_asset_adapter` | `mcp/rpg_adapter` |
| `res://addons/dv_checker` | `mcp/editor_lsp/godot` |

## 生成与证据边界

[资产管线](asset_pipeline/README.md)、[音乐管线](music_pipeline/README.md)、[编辑器](editor_lsp/README.md) 和 [采集诊断](verification/README.md) 维护各自的输入与输出合同。Blender、MuseScore、Godot 和 .NET 依赖由所选工具决定。原始创作文件与原始证据保留；一次性脚本只放 `.scratch/`，任务结束时清理。

工具完成与领域结果分开表达。逻辑记录的 `evidence_complete` 描述证据封存；`causal_check_complete` 采用 70-checks 实际领域分析完成标志；`main_causal_supported` 单列主因果结论。负面判断可来自已完成分析，未进入分析则给出明确原因。当前用户禁止执行时，所有这些工具只维护源码和路由。
