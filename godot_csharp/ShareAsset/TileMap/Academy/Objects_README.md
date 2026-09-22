# 学院分镜扩充素材 v1

参考 `artifacts/theme-film/scenes/shot-03.png`、`shot-19.png` 和现有 Victorian_Inside_B.png，以内置 imagegen 生成并迭代了16组陈设。提示词保存在本目录 prompts.md，最终生成原稿保存在 source/。

## 可用文件

- `academy_objects_48.png`：768×768 RGBA 图集，48×48格，16×16格；每个组件占4×4格。行优先顺序与 catalog.json 一致。
- `Academy48.tres`：现有 Victorian TileSet 的独立扩展版本，保留原地形，增加本图集；可供 TileMapLayer 绘制。
- `academy_objects_48.res`：由同一 PNG 内容生成的 Godot ImageTexture，供 TileSet直接引用，无需等待 PNG 导入。
- `catalog.json`：组件名称、图集格坐标、尺寸、原稿裁切范围及 source_id。

第一行是书架、点燃壁炉、沙发、研究桌；第二行是钢琴、座钟、挂旗、望远镜；第三行是关闭石门、开启石门、竖铜管、弯铜管；第四行是铭文基座、炼金台、茶桌、壁灯。火焰及开关门均为静态图。铜管为独立陈设，尚未提供无缝管网连接规则。图案沿用本轮分镜概念，不作为正式身份纹章。

## 原生像素限制

生成工具两次均输出1254×1254，未遵守768×768要求。本版新陈设是经测量裁切、最近邻缩小并整理到48px网格的适配素材，**不是原生48px逐像素绘制**。原有 Victorian 地形仍直接使用原资源。不能以图集尺寸可被48整除代替原生像素美术验收；若必须严格原生绘制，新陈设仍需逐像素重绘。

## 场景与使用

打开 `res://scenes/AcademyTileset/Parlor48.tscn` 或 `Corridor48.tscn`，可编辑地面、墙面、地毯和家具层。地图24×16格，1:1显示；项目默认960×640视口显示中心区域，完整地图为1152×768。示例用于检查拼装，未添加角色与交互。

在 TileSet 图集中框选一个完整4×4区域作为组件放置。透明格也会写入同一图层，重叠陈设应使用另一图层，避免后放组件擦除先放组件。保持最近邻过滤，不启用有损压缩。

生成入口：`res://tools/build_academy_tileset.gd`。执行该脚本会重新生成本目录的派生资源和两个示例场景；手工修改前另存副本。传入用户参数 `--capture` 可同时保存1152×768渲染图。验证记录在 `artifacts/academy-tileset-validation/`。
