# 游戏运行资源

本目录保存 Godot 运行时读取的纹理、模型、音频、字体、材质、资源清单及随附来源和许可证。游戏使用 `res://ShareAsset/...` 访问这些成品。

| 内容 | 维护位置 |
| --- | --- |
| 美术生成器、导出器与配方 | `mcp/asset_pipeline/` |
| 可编辑原始美术与制作资料 | `mcp/asset_pipeline/sources/` |
| RPG 资源导入适配器 | `mcp/rpg_adapter/` |
| 曲谱、音乐编配、音频制作与专用采样库 | `mcp/music_pipeline/` |
| 发布的运行资源 | `godot_csharp/ShareAsset/` |
| 地图物理坐标与补充几何 | `godot_csharp/Contracts/geometry_contract.json` |

资源生成命令从仓库根目录调用相应 `mcp` 脚本；它们把成品写回本目录。需要 Godot 编辑器插件或独立资源工具时，使用 `python mcp/godot_workspace.py --editor` 启动链接现役资源的工具项目。正式游戏入口位于 `godot_csharp/scenes/Host/Main.tscn`。

迁移保留原始资料与现役成品。已发布场景、纹理或清单也可能是后续组合工具的输入；具体关系见各资源目录说明。外部素材的来源、许可和分发要求保留在原资源旁。
