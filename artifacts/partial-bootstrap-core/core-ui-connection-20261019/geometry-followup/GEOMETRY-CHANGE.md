# 地图物件与站点：生成源静读

2026-10-19。仅修订 `godot_csharp/tools/chapter2_art/rooms.py` 与 `godot_csharp/tools/build_chapter2_scenes.py`。内容位置由主代理依据设计规范核定；本分工未读取设计文档。原 `.story` 的物件贴图、站点与旧家具配方仅用于坐标和资产溯源，不恢复旧场景或执行路径。

## 源码范围

仅 `zone_yi` 增加几何与物件声明。全部使用仓库既有 PNG。通用生成器新增显式 `res://` PNG 路径、纹理裁片、绘制偏移、旋转与 z_index 的资产/几何能力；生成器没有新增对白、证物正文或设计结论。

新增 `--geometry-only zone_yi` 模式，从地图配方及既有 PNG 重新生成目标地图与 manifest，不重画纹理，不导入 Godot。普通文本站点与无文本 `geometry_sites` 分开声明，后者的场景 label 留空，由 native 呈现提供玩家标题。

## 精确物件声明

以下坐标为当前 zone_yi 地图局部像素。所有未特别注明的节点 z_index=0，使用现有 Objects 的 Y-sort。

| 节点 | 既有 PNG | 节点坐标 / 排序位置 | 绘制与碰撞 |
|---|---|---|---|
| InnerBolt | AwakeningEvidence/isolation_door_side.png | (22,422) | region=(74,86,8,48)，offset=(-4,-48)，实际裁片左上=(18,374)，完整保留纵闩与闩座；无新增 solid |
| AshFan | AwakeningEvidence/ash_fan.png | (31,378) | offset=(-50,-34)，旋转90°，z_index=-1；灰扇尖端位于当前门内侧(32,390)，向东内侧展开；无新增 solid |
| Wedge | AwakeningEvidence/wedge.png | (62,428) | offset=(-7,-8)，绘制左上=(55,420)，贴近同门灰扇及南侧墙角；无新增 solid |
| Score | AwakeningEvidence/score.png | (522,228) | 与现N2Desk排序位置相同，offset=(-9,-34)，绘制左上=(513,194)，底部207；无新增 solid |
| Slate | AwakeningEvidence/slate.png | (368,296) | 与StudyTable排序高度相同，offset=(-10,-44)，绘制左上=(358,252)，底部266；无新增 solid |
| Jar | AwakeningEvidence/jar.png | (368,536) | 与Sideboard排序高度相同，offset=(-7,-48)，绘制左上=(361,488)，底部506；无新增 solid |
| R05Shelf | FirstAwakeningInterior/bookcase_empty.png | (800,110) | 新增 solid=(780,96,40,14)，位于原Wardrobe和Bed之间，避开原先提案(878,110)与Bed重叠的问题；远离y256房门 |
| R05ShelfTrace | AwakeningEvidence/shelf_dust_hand.png | (800,110) | 既有40×60架面叠图，与空架同锚点、同排序高度，排列在架后绘制；无新增 solid |

E1只使用原图内闩裁片，不用整扇门覆盖当前门体，也没有新增开关门动画或修改门禁。E2木楔与灰扇均放在当前隔离门内侧同一局部空间，没有沿旧场景坐标保留相隔较远的物件。

## Native 绑定接口

| Site marker | 坐标 | 当前地图 room |
|---|---|---|
| site:E1 | (26,396) | zone_yi:foyer |
| site:E2 | (62,422) | zone_yi:foyer |
| site:E6 | (522,202) | zone_yi:north_2 |
| site:E7 | (368,258) | zone_yi:hall |
| site:E10 | (368,502) | zone_yi:prep |
| site:E11 | (806,110) | zone_yi:north_1 |

物件表面的 marker 不等于可站立脚点；正式执行仍由现有站点半径、entered_room、寻路与碰撞选取合法接近点。门闩 marker 位于门体，目标主体须在门厅内实际接近。

## 当前证据与限制

已静读现有地图、配方、物件贴图源、`.story` 的物件坐标和热点绑定；实际查看了 desk_0、study_table、isolation_door_side 与 shelf_dust_hand 的既有 PNG。

按主代理随后授权，执行 `python godot_csharp/tools/build_chapter2_scenes.py --geometry-only zone_yi`，正常返回；命令、stdout/stderr与生成前后PNG指纹保存在 `generation-01.json`、`generation-01.stdout.log`、`generation-01.stderr.log`。生成前后比较原场景引用PNG、AwakeningEvidence及FirstAwakeningInterior既有PNG，changed_png为空；场景底图也在指纹记录内。源码明确只发布目标场景及manifest。

已静读实际生成的 zone_yi.tscn Git差异：保留原物件、地形、出口、Spawn与Stand，新增上述物件和marker，新增书架矩形s151（中心800,103，尺寸40×14）；manifest记录新站点与geometry_objects。没有其他地图生成。

未启动构建、import、探针、正式会话或游戏，也未执行 flow。仍须实际核对地图绘制、可达位置、碰撞与 UI；静态坐标关系与生成完成不构成现场验收。
