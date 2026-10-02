# TileMap工作流运行时资源

源：godot_csharp/AssetsSource/Internal/Props/TileMapWorkflow。本族为旧47形TileMap样板，由 `godot_csharp/tools/rebuild_first_awakening_tilemap.py` 通过 RPG TileMap MCP 重建并调用 `export_godot`；文件名不表示它仍是初醒地图入口。`addons/rpg_asset_adapter/mcp/demo.py` 会覆盖本族样板资产，不用于当前初醒重建。

atlas.png包含47形墙顶、地面与上下墙面；tiles.json登记格坐标、碰撞和邻接；build.json供Godot导出器使用。纹理.res与TileSet由export_map.gd保存，不手改派生资源。场景入口为scenes/TileMapWorkflow/Preview.tscn。

build.json的doors登记方向与完整门洞区域，Godot验证沿正确方向穿过门洞。MCP的spatial报告含角色脚底扫掠与已提交家具占地；当前建筑导出的props为空，不能用它声称FirstAwakening家具已经验收。

当前 `FirstAwakening.tscn` 使用 `ShareAsset/Internal/Props/FirstAwakeningInterior/life_wing.png` 与 `layout.tres`，不实例化本族Preview或读取本族build.json；其重建入口为 `godot_csharp/tools/build_first_awakening_interior.py`。本样板的尺寸与空间检查不能代表当前初醒验收。技术和美术验收见 [统一设计规范](../../../../docs/design-standard.md#第五部分32px-像素美术与地图工作流)。
