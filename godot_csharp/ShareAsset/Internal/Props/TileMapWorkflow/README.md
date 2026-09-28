# TileMap工作流运行时资源

源：godot_csharp/AssetsSource/Internal/Props/TileMapWorkflow。当前地图由 `godot_csharp/tools/rebuild_first_awakening_tilemap.py` 通过 RPG TileMap MCP 重建，并自动调用 `export_godot`。`addons/rpg_asset_adapter/mcp/demo.py` 属旧样板配方，运行会覆盖当前初醒地图，不应用作本场景重建入口。

atlas.png包含47形墙顶、地面与上下墙面；tiles.json登记格坐标、碰撞和邻接；build.json供Godot导出器使用。纹理.res与TileSet由export_map.gd保存，不手改派生资源。场景入口为scenes/TileMapWorkflow/Preview.tscn。

build.json的doors登记方向与完整门洞区域，Godot验证沿正确方向穿过门洞。MCP的spatial报告含角色脚底扫掠与已提交家具占地；当前建筑导出的props为空，不能用它声称FirstAwakening家具已经验收。

`FirstAwakening.tscn` 实际实例化上述 Preview 的建筑层，移除预览人物；运行时从本目录的 `build.json` 读取可走地面格。素材族混合原生像素命令与既有生成派生家具，尺寸合规不等于全部原生绘制。技术和美术验收见docs/tilemap-workflow.md。
