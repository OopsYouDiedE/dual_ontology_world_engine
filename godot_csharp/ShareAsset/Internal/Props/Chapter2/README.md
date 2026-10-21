# 统一地图运行资源

`scenes/Chapter2/Field/CampusMapView.tscn` 与 `CampusMapView.gd` 是正式多地图适配器，按 DV 呈现绑定加载本目录及 `scenes/Chapter2/Maps/` 的资源。统一地图承接开局寝区、公共室内与室外校区；目录名不表示它只在某一章才被使用。

| 路径 | 运行用途 |
| --- | --- |
| `rooms/<地图键>.png` | 建筑与地面底图，供地图场景的 Architecture 使用 |
| `rooms/<地图键>.relief.tres` | CampusMapView 按底图基名动态载入的 CanvasTexture |
| `rooms/<地图键>-normal.png` | 上述 CanvasTexture 的法线贴图 |
| `rooms/relief-manifest.json` | 法线生成方式及底图指纹说明 |
| `props/<名称>.png` | 地图物件、建筑、植被与室外小件 |
| `manifest.json` | 运行时读取的地图路线、出生点与尺寸等数据，同时记录资源/几何信息 |
| `godot_csharp/scenes/Chapter2/Maps/<地图键>.tscn` | 底图、物件、碰撞、出口、出生点和站点 |

现役室内地图包括四个寝区、起居厅、中央厅、讲堂、档案室、配重室、共振室、医务室、后勤区、粮仓与温室；室外地图为 `campus_nw`、`campus_n`、`campus_ne`、`campus_sw`、`campus_s`、`harbor`。室外使用共有全局坐标、重叠分区、核心范围和原点；切图保留实际身体与位置。可达性、门禁及行动结算仍分别由真实几何和 DV 正式动作合同约束。

第一章共享家具直接引用 `ShareAsset/Internal/Props/FirstAwakeningInterior/`，证物直接引用 `AwakeningEvidence/`。这些目录中的现役贴图并非本目录的冗余副本。代码还会从 DV 几何声明动态创建物件、站点与碰撞；字面场景引用不是完整依赖清单。

## 生产路径与重建前提

生活翼的正式执行地图为 `Maps/living_floor.tscn`。`tools/build_living_floor.py` 从四寝区及起居厅已发布场景合成该文件，直接引用原 PNG/CanvasTexture/法线与家具资源，不另画底图；五张源场景仍是重建输入，不能作为未使用成品删除。公共厅中段补接六格，其余场景仅平移，门禁改为同图屏障。改动源场景后重新运行此组合工具；室外分区和其他建筑维持原样。

布局和绘制工具保留在 `godot_csharp/tools/chapter2_art/` 与 `build_chapter2_scenes.py`。仍被现役生产器调用的共用纯绘图模块位于 `tools/first_awakening_art/`；其目录名不构成旧剧情执行入口。地图标题和站点标签读取已有 manifest 中记录的文本，不再要求退役场景的编译 JSON。

局部 `--text-only`、`--sites-only`、`--geometry-only` 模式使用现存 manifest、场景和 PNG；`--interiors-only` 另使用共用绘图配方重画室内与烘焙阴影。几何生成将家具 footprint 转成碰撞，核对出入口目标、出生点及来源标签。文本来源缺失时应报告具体缺口，不能从标识猜测剧情文案。

完整室外重建还需要校区地形和选址源数据，当前仓库未包含这组原始输入。通过 `--site-source` 提供实际源目录，入口在任何资产写出前检查来源齐备；不要用猜测地形或默认占位数据覆盖现有地图。缺源时保留已交付的 PNG、TSCN、manifest 与导入参数。

任何重建都应先明确来源，保留已有几何/资源指纹，按实际改动范围检查导航、门洞、阴影和动态呈现绑定。本次清理保留现有正式场景与纹理原字节；工具链整理与源模块恢复不构成视觉一致性或实机到达的验收结论。

`tools/build_surface_materials.py` 从现有底图生成法线与 CanvasTexture，保持 diffuse 像素不变。法线表现的是近似表面起伏，不改变物理几何；删除法线或 `.relief.tres` 会改变当前光照效果，不能当作无用缓存清理。

## 查看与工程检查

正常地图游玩通过正式 `Host/Main.tscn` 入口启动，命令及验收边界见 `godot_csharp/AGENTS.md`。需要开发整图截图时，使用：

```powershell
& $env:GODOT --path godot_csharp --rendering-method gl_compatibility --script res://scenes/Chapter2/Field/capture_maps.gd
```

该工具逐张加载地图，并以现役 PixelCast 侦探显示比例，截图写到 `tmp/chapter2_capture/`。它不打开正式 DV 会话，不能证明剧情、主体选择、正式移动回执或完整游玩效果。资源变更后的重新导入和实机检查遵循工程约束，旧浏览/回归入口不再作为运行命令。
