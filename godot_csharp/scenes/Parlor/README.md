# 公共娱乐室地图

在 Godot 中打开 `Parlor.tscn`，按 F6 运行。地图使用项目现有插件生成的 `ShareAsset/victorian/tilesets/Victorian.tres`，为30×20格、每格48px的可编辑场景。

参考为 `artifacts/theme-film/scenes/shot-03.png`：上方壁炉、中央会客区、两侧茶桌、左下钢琴和下方入口。素材没有原画同款书架、人物和点燃壁炉效果，使用现有柜体及家具做布局适配，没有把原画当地图背景。

Floor、Walls、WallCaps、Rugs、Furniture、TabletopObjects 六层共享原 TileSet。颜色调整保存在图层上，桌面物件上移12px。所有图块均可继续编辑。

生成入口为 `res://tools/build_parlor.gd`。验证渲染和记录位于仓库的 `artifacts/parlor-validation/`。重新运行生成脚本会覆盖本场景，手工编辑后请先另存副本。

默认游戏视口为960×640，相机缩放为1:1，一屏约20×13格，初始聚焦中央会客区。整张地图仍为30×20格，不缩小图块来塞满窗口。

本次 Godot 4.6.1 实测：保存后重新加载得到752个有效图块，5,184项地形邻接检查通过；960×640实际渲染已检查。此结论限于本场景使用的转换能力。
