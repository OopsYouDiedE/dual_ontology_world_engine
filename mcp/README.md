# 可复用工具中心

正式游戏资源和脚本位于 `godot_csharp/`；工具、原始资产及编辑器插件集中在本目录。所有命令从仓库根目录执行。

## 按需 MCP

```powershell
python mcp/server.py
```

服务通过 stdin/stdout 接收一行一个 JSON-RPC 消息。`initialize` 与 `tools/list` 只读取 `tools.json`；`tools/call` 才导入对应实现。工具包括资产管线、音乐管线、编译器预热、CLI、地图/编辑器工作区、RPG 适配器和诊断入口。工具运行输出写入响应或 stderr，stdout 保持协议流。

| 目录 | 内容 |
| --- | --- |
| `build/` | 自举编译器、模块缓存与分区代码生成 |
| `asset_pipeline/` | 地图、角色、3D书桌、材质、UI 和几何契约导出 |
| `asset_pipeline/sources/` | 可编辑原始资产；成品发布到 `godot_csharp/ShareAsset/` |
| `music_pipeline/` | 谱面、SF2 合成与音轨生成 |
| `rpg_adapter/` | 编辑器适配器及图像/像素/工作流工具 |
| `editor_lsp/` | DV 语言服务器、检查入口与 VS Code/VS/Godot 客户端 |
| `sandboxes/` | 纯 GDScript MapSandbox 和工具生成预览 |
| `verification/` | 二进制记录读取、采集、分析与独立检查 |

## Godot 工具工作区

```powershell
python mcp/godot_workspace.py --sandbox --godot $env:GODOT
python mcp/godot_workspace.py --editor --godot $env:GODOT
python mcp/godot_workspace.py --csharp --godot $env:GODOT --script res://tools/capture_title_3d.gd -- --out=<新目录>
```

工作区位于 `.scratch/`，链接正式场景、成品、契约和工具目录。编辑器模式安装两个工具插件；C# 模式生成独立 `DuelView.Tools` 工程，额外编入 FrameArchive。工具退出时清理工作区。地图沙盒不加载 C# 或 DV。工具生成的预览场景放在 `sandboxes/generated/`，正式场景只保留运行依赖。

## 常用生成命令

```powershell
python mcp/asset_pipeline/build_living_floor.py
python mcp/asset_pipeline/export_geometry.py
python mcp/asset_pipeline/build_ui_theme.py
python mcp/asset_pipeline/build_pixel_cast.py --only detective
python mcp/music_pipeline/build_soundtrack.py
```

`build_painterly_desk.py` 与 `build_desk_surface_maps.py` 在 Blender 中执行。MCP 资产入口使用 `BLENDER` 环境变量或 PATH 中的 Blender；图像和音乐依赖在各管线文档中说明。生成过程读取迁移后的 sources，保留原始创作文件。单次批处理脚本使用 `.scratch/`，可复用能力直接扩展对应工具函数。

## Core 入口

```powershell
python mcp/build/build_session.py --partition --out dv_core/host_bridge/obj/dv
python mcp/editor_lsp/check_dv.py --format json dv_core/modules/30-session.dv
python mcp/editor_lsp/dv_lsp.py
```

65-debug-runner 与 70-checks 独立于生产。逻辑运行、取证与检查的使用范围见 [verification/README.md](verification/README.md)；新原始记录保持二进制格式。执行时机以当前任务授权为准。
