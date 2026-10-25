# 资产生成与发布

本目录维护地图、角色、UI、材质、原始素材和几何契约的生成入口。所有命令从仓库根目录运行；统一 MCP 工具是 `asset_pipeline`，`operation` 枚举直接来自 `mcp/tools.json`。

| 工作 | MCP operation | 源与输出 |
| --- | --- | --- |
| Astra大模块舞台 | `modern-stage` | `publish_astra_stage.py`切分`sources/Internal/ModernStage/Astra/`的认可源板，发布`Props/ModernStage/`的地板、墙、桌椅、四姿态SpriteFrames、干木杖接触音与逐项裁切/变换清单；不重绘。模块宽128px且纵向等比，物理保持32px尺度。 |
| 文档纹理体系 | `document-textures` | 三代建筑材质、工业/纸皮、限定视觉及九组透明徽记；制作说明在 `sources/Internal/Textures/DocumentSystems/DESIGN.md`，成品在 `ShareAsset/Internal/Textures/DocumentSystems/`，无参数 |
| 地图 | `living-floor`、`chapter-maps` | 组合现有场景、显式地图文字和原始室外素材，发布 Chapter2 场景与资源 |
| 角色 | `anime-cast`、`pixel-cast`、`dialogue-cast`、`character-atlas` | 读取对应原稿，发布角色资源 |
| 书桌与材质 | `desk`、`desk-surfaces`、`room-props`、`scene-surfaces`、`materials` | Blender 或图像制作源，发布几何、纹理与材质 |
| UI | `ui-theme`、`ui-paper`、`ui-desk`、`translations` | 主题、纸纹、书桌与本地化资源 |
| 选定原稿发布 | `publish-reference-desk`、`publish-redrawn-cast`、`publish-geometry-assets` | 按各发布器的原稿、复核记录和目录参数发布成品 |
| 共享几何合同 | `geometry-contract` | 从场景提取锚点、出口、门禁与室外坐标，保留合同中已声明的房间区域和连接 |
| 高光舞台物件与人物 | `highlight-stages` | `build_highlight_stages.py` 裁切已保留的 ImageGen 透明原稿，发布分类道具 PNG / AtlasTexture 和 32×48 三向人物 atlas；不启动游戏或会话 |
| 正式房间地面层 | `highlight-room-skins` | `publish_room_terrain_skins.py --maps central_hall granary` 接入原稿采样层；覆盖格来自原 terrain_rows，纹样采用独立源坐标、统一缩放与相位。章节地图再生复用同一函数 |
| 房间原稿精确裁切 | `authored-room-surfaces` | `publish_authored_room_surfaces.py` 归档中央厅、粮仓与诗人悬廊原图，发布不重采样的地面、边饰和纹盘裁切；需要 Pillow，不启动 Godot |
| 参考建筑模块 | `reference-facade` | `publish_reference_facade.py` 裁切 ImageGen 正交原稿，发布32px基础格与跨格墙/基座/窄窗/门/檐/转角模块，并接入讲堂南立面；需要 Pillow，不启动 Godot |

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

`python mcp/asset_pipeline/publish_highlights_rpg.py`（MCP `asset_pipeline/highlights-rpg`）发布首批01诗人、06一楼门口RPG资源。目标/无人板源在`Internal/Scenes/HighlightsRpg`，人物源在`Internal/Characters/HighlightsRpg`，独立货物和源质源在`Internal/Props/HighlightsRpg`。运行目录遵循相同人物/道具分类；`Props/HighlightsRpg/publication.json`登记每层原图区域、等比尺度、世界原点、脚点、遮挡及姿态纹理，`layout.json`登记局部物理cue与镜头跟随接口。

1448×1086无人板等比发布为1024×768分区，逻辑512×384、32格。区域包含独立地面、墙面、门口侧面、前景栏杆、平台/阶梯；它们是指定构图的固定区域，不冒充通用自动地形。门扇透明像素摘取仅供Y排序遮挡，底板仍保留静态开门像素，不能单独移动门扇。人物通过透明连通体切分新原稿，发布96px高、32色无抖动姿态；64×96身体基准之外保留持剑/展开手臂的透明跨格画布，`foot_px`与0.5缩放决定精确世界脚点，不能按每张宽度重新居中。短姿态组不宣称完整多方向步态。

01原稿纹样裁切、7/24比例和相位另在layout的`ornament_source`登记；ImageGen重投影地面不是逐像素原稿，组装与独立对照须保留这一区别。06只在一楼门内发现玄武岩门前平台迫近源质，目标、运行静态层和cue不含旧上层吊篮构图。发布器不运行Godot、不写生产场景，组装与真实渲染验收另行记录。

`ReferenceFacade` 原稿位于 `sources/Internal/Props/ReferenceFacade/master.png`，目标图为 `artifacts/campus-architecture-review/astra/12_settlement_material_refinement.png`，提示词保存在同源目录 `prompt.txt`。正交组件依该图主楼的暖象牙墙、灰石基、深木窄窗与黑瓦檐制作，由 ImageGen 绘制，不是对目标图的透视立面截图。发布器只裁切与等比nearest-neighbor缩放；运行资源在 `ShareAsset/Internal/Props/ReferenceFacade/` 按Wall、Foundation、Openings、Cornice、Corners、Roof分类，配有TileSet与逐组件来源清单。

现役用途为 `campus_n.tscn` 与 `campus_nw.tscn` 中同一讲堂的 `Objects/讲堂/ReferenceFacade`：在原Sprite脚点、尺寸、屋顶主体与碰撞之内组合南立面，跟随原建筑遮挡淡出。讲堂南侧原有六窗且无门，本次不新增入口；门组件作为独立资源保留。章节地图生成器复用同一个attach函数，避免重建丢失接线。整体建筑形制仍是现役布局，只材料与组件依目标图，不能声称整栋建筑与参考一致。实际渲染对照须观察讲堂南立面及分区交接，不以图集预览替代。

中央厅、粮仓与诗人悬廊优先使用 `Props/AuthoredRooms/publication.json` 声明的各自房间原稿。源图归档在 `sources/Internal/Props/AuthoredRooms/`，成品在 `ShareAsset/Internal/Props/AuthoredRooms/`。`AuthoredSurface.gd` 以完整裁切图的比例采样，32px覆盖格不改变纹样大小，也不切掉砖角；世界相位在全部覆盖区域连续。象牙方框材质不再代替诗人原稿的菱形地纹。

中央厅采用 `artifacts/chapter12-room-design/12-central-hall.png`，粮仓采用同目录 `06-granary.png`，诗人采用 `sources/Internal/Props/Highlights/Backgrounds/shura_gallery.png`。原图有光影、透视和非周期变化；发布器保留这些像素，裁切重复处不宣称无缝。中央厅柱体遮挡处不能恢复原有径纹，纹盘单独隔离可见像素，内部遮挡区使用同图青灰盘面样本，缺失的北侧金圈弧不补画；此限制写入publication。其余五幕没有对应原稿确认，本批仍沿用下述旧地形，不宣称已达到原稿一致性。

`python mcp/asset_pipeline/publish_highlight_tiles.py --source <4x4材质原图>` 提取完整材质格，以 nearest-neighbor 发布32×32原料，再派生floor47角边、wall16四边、water47岸缘。遮罩保留中心16×16；四边使用16宽连接臂；47格式角位独立控制8×8角，16格式由相邻两边决定角。清单保存源格、比例、SHA-256、地形索引、peering bits和透明边预览。成品为 `godot_csharp/ShareAsset/Internal/Props/Highlights/Terrain/HighlightsTerrain.tres` 与外置PNG。已逐格绘制的原生图集可用 `--format blob47|wang16|minimal3x3 --floor-source <PNG> --wall-source <PNG> --water-source <PNG>`，分别要求8×6（47格、余格空）、4×4或3×3；3×3为手工变体。`--godot <程序>` 指定生成器使用的Godot。
