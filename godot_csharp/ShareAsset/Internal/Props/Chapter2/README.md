# 第二章场景运行时资源

全部由原生 1x 像素配方逐像素绘制，没有源图缩放：

- 建筑底图配方：`godot_csharp/tools/chapter2_art/painter.py`（复用第一章 `first_awakening_art` 的材质、墙面、墙顶与阴影画法），场景布局数据在 `rooms.py`；
- 室外：`outdoor.py`（地面、海岸、路、围墙、植被、可走范围与六区切分）、`buildings.py`（建筑体块、立面、屋面、穹顶与塔）、`oblique.py`（三分之四投影与逐面光栅化）、`nature.py`（树、绣球、礁石）。数据取自 `artifacts/remake-chapter2/campus-site/`（科尔沃岛真实地形缩 0.5 倍、选址、建筑表与路网）。比例 1 m = 24 px，地面正投影、立面同比例竖直画出，光从西南上方来；
- 第二章家具与外景小件：`props_core.py`、`props_archive.py`、`props_service.py`；第一章家具直接引用 `FirstAwakeningInterior/` 下的现有贴图，不复制。

重建：在仓库根目录执行 `python godot_csharp/tools/build_chapter2_scenes.py`，再执行 `godot --headless --editor --path godot_csharp --import`。输出：

| 路径 | 内容 |
| :--- | :--- |
| `rooms/<场景>.png` | 每个场景的建筑底图（地面、墙面、墙顶、门框、窗、壁灯、地毯与阴影） |
| `props/<名称>.png` | 第二章精灵；`outdoor_bldg_*` 建筑、`outdoor_wall_*` 围墙与路墙分段、`outdoor_{round,conifer,hydrangea,faya,boulder}_*` 植被与礁石、`outdoor_stele` 契约石碑 |
| `manifest.json` | 精灵尺寸、占地、SHA-256，场景出入口、出生点与调查点 |
| `godot_csharp/scenes/Chapter2/Maps/<场景>.tscn` | 每个场景一份：底图、按脚底排序的物件、墙与家具碰撞、出入口 Area2D（目标场景与出生点写在元数据）、出生点与调查点 Marker2D |

精灵的锚点是画布底边中点（脚底），碰撞取配方给出的脚底占地，不用整张透明画布。构建脚本会断言每个出入口的目标场景与出生点都存在。

室外六区（`campus_nw`、`campus_n`、`campus_ne`、`campus_sw`、`campus_s`、`harbor`）共用一个全局像素坐标系，彼此重叠 24 m；每张图的元数据记有本区原点、核心范围与全部区域的核心与原点。浏览脚本在侦探走出本区核心（加 36 px 回差）时换到所在区域并保持世界坐标。建筑与树冠带 `occluder` 元数据，侦探走到其背后时半透明。室内出口在 `rooms.py` 里写作 `campus`，构建时改指向出生点所在的区域。完全落在不可走区域上的植被与牧场石墙直接烘焙进地面图。

浏览与自检（场景层，不含剧情系统）：

- 行走浏览：`godot --path godot_csharp res://scenes/Chapter2/Field/Chapter2Field.tscn`（WASD 或方向键；可加 `-- --map=campus_n --spawn=hall_south` 指定起点）；
- 出入口与区域接缝回归：`godot --headless --path godot_csharp --script res://scenes/Chapter2/Field/check_transitions.gd`；
- 整图截图（开发自检，输出到仓库根目录 `tmp/chapter2_capture/`，不提交）：`godot --path godot_csharp --rendering-method gl_compatibility --script res://scenes/Chapter2/Field/capture_maps.gd`。

技术生产路径不等于艺术验收通过；第二章 12 名新角色的精灵尚未制作，浏览场景只放侦探。
