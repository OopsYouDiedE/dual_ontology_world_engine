# 资产生成与发布

本目录维护地图、角色、UI、材质、原始素材和几何契约的生成入口。所有命令从仓库根目录运行；统一 MCP 工具是 `asset_pipeline`，`operation` 枚举直接来自 `mcp/tools.json`。

| 工作 | MCP operation | 源与输出 |
| --- | --- | --- |
| 文档纹理体系 | `document-textures` | 三代建筑材质、工业/纸皮、限定视觉及九组透明徽记；制作说明在 `sources/Internal/Textures/DocumentSystems/DESIGN.md`，成品在 `ShareAsset/Internal/Textures/DocumentSystems/`，无参数 |
| 地图 | `living-floor`、`chapter-maps` | 组合现有场景、显式地图文字和原始室外素材，发布 Chapter2 场景与资源 |
| 角色 | `anime-cast`、`pixel-cast`、`dialogue-cast`、`character-atlas` | 读取对应原稿，发布角色资源 |
| 书桌与材质 | `desk`、`desk-surfaces`、`room-props`、`scene-surfaces`、`materials` | Blender 或图像制作源，发布几何、纹理与材质 |
| UI | `ui-theme`、`ui-paper`、`ui-desk`、`translations` | 主题、纸纹、书桌与本地化资源 |
| 选定原稿发布 | `publish-reference-desk`、`publish-redrawn-cast`、`publish-geometry-assets` | 按各发布器的原稿、复核记录和目录参数发布成品 |
| 共享几何合同 | `geometry-contract` | 从场景提取锚点、出口、门禁与室外坐标，保留合同中已声明的房间区域和连接 |
| 高光舞台物件与人物 | `highlight-stages` | `build_highlight_stages.py` 裁切已保留的 ImageGen 透明原稿，发布分类道具 PNG / AtlasTexture 和 32×48 三向人物 atlas；不启动游戏或会话 |
| 正式房间地面重绘层 | `highlight-room-skins` | `publish_room_terrain_skins.py --maps central_hall granary` 接入地面 TileMap；格坐标来自原 terrain_rows，保留镶嵌纹样、独立家具、几何和门口。章节地图再生复用同一函数 |

高光舞台的可复用资源和独立场景说明见 `../../godot_csharp/scenes/Highlights/README.md`。`sources/Internal/Props/Highlights/prompts.json` 保存 built-in ImageGen 提示词；人物的比例修订另记在 `sources/Internal/Characters/Highlights/cast_revision_prompt.json`。原稿裁切、透明边界与 nearest-neighbor 发布比例写入成品 manifest，实际画面效果另行观察。背景原稿保留为制作参考，运行地图使用 TileMap 与独立道具装配。

`arguments` 是脚本原有参数列表。书桌的两个 Blender 入口读取 `BLENDER` 环境变量或 PATH。角色发布的 `--source`、`--review` 等输入保持显式；缺少原稿时先补齐原稿。地图生成器的 `--site-source` 和文字输入遵循其命令参数，已有发布成品不会被当作缺失原稿的替代来源。

## 目录和共享库

- `sources/` 保存原始图片、模型、制作参数、来源记录与许可；`godot_csharp/ShareAsset/` 保存游戏读取的成品。
- `first_awakening_art/` 保存共享像素绘图、调色和家具/材质函数；`chapter2_art/` 组合这些函数制作当前地图。RPG MCP 的 `native_art_catalog`、`read_native_recipe` 和绘图操作调用同一组模块，并报告实际源码哈希。这些库不提供完整旧场景或缺失角色原稿的重建入口。
- `asset_sources.py`、`map_text.py`、`scene_geometry.py` 是共享读取库，通过上表的工具入口使用。
- `capture_maps.gd` 通过 `godot_tools` 的 `capture-maps` 或临时 Godot 工程调用，采集输出保存在用户指定的任务目录。

## 几何发布

`export_geometry.py` 与独立 Python 几何检查入口共用 `scene_geometry.py` 读取场景。合同包含 markers 的 point/path/room、全部出口 Area2D 的 point/path/rect/target/spawn/internal_gate/gate，以及场景声明的 `outdoor_origin`、`outdoor_core`、`outdoor_cores`、`outdoor_origins`。内部门禁保留在出口清单中，跨地图路由只使用有目标的外部门。

场景中的旧外部目标 `living_hall` 在发布时映射到 `living_floor`，目标出生点显式加 `living_hall_` 前缀；`doors` 的对应 target_map/to 同步更新。`living_hall:hall` 等内部房间标识保持原义。运行端读取发布合同，不再重复推导这项映射。

独立静态入口是 `diagnostics` 的 `geometry`；Godot MapSandbox 使用同一合同核对实际加载场景。它们只在明确调用时运行。生成、静态对照、物理运动和人工画面观察各自报告其结果。

## Highlights 地形发布

`python mcp/asset_pipeline/publish_highlight_tiles.py --source <4x4材质原图>` 提取完整材质格，以 nearest-neighbor 发布32×32原料，再派生floor47角边、wall16四边、water47岸缘。遮罩保留中心16×16；四边使用16宽连接臂；47格式角位独立控制8×8角，16格式由相邻两边决定角。清单保存源格、比例、SHA-256、地形索引、peering bits和透明边预览。成品为 `godot_csharp/ShareAsset/Internal/Props/Highlights/Terrain/HighlightsTerrain.tres` 与外置PNG。已逐格绘制的原生图集可用 `--format blob47|wang16|minimal3x3 --floor-source <PNG> --wall-source <PNG> --water-source <PNG>`，分别要求8×6（47格、余格空）、4×4或3×3；3×3为手工变体。`--godot <程序>` 指定生成器使用的Godot。
