# TileMap工作流源与运行时关系

当前初醒地图唯一重建入口：godot_csharp/tools/rebuild_first_awakening_tilemap.py；通用工具见godot_csharp/addons/rpg_asset_adapter/mcp/README.md。demo.py是保留的旧样板配方，会覆盖本族现行地图，不得用作初醒重建。

*.drawing.json与同名PNG是整数像素绘制源；*.adapt.json记录源哈希、裁切、原生格、目标画布、缩放和留白。tiles.recipe.json记录材质与边饰；map.json记录房间、道具锚点与碰撞。

当前墙顶、暖灰木地板、冷灰蓝矿物灰泥墙面与木护墙板来自重建脚本的32px整数绘制命令，配方同步保存在对应drawing.json。墙面已去除等距高亮砖缝，以低对比斑驳表现材质。study_a4_material.png/study_a5_material.png是历史材质副本，保留来源，不再作为现行材质裁切输入。旧样板的窗户地毯源及guest.original.png同样保留，不代表当前地图正在使用。map.json登记8处门洞的朝向及贯穿墙厚的格矩形；当前props为空，实际家具由FirstAwakening拥有。

派生在godot_csharp/ShareAsset/Internal/Props/TileMapWorkflow，验收场景scenes/TileMapWorkflow/Preview.tscn。MCP空间检查与实机图归artifacts/tilemap-workflow；建筑截图不代表游戏家具或艺术质量通过。

建筑轮廓使用地板八邻接补齐拐角，北侧墙面只占各宿舍室内宽度，外墙和隔墙列保留墙顶并连接顶部墙沿。不要恢复四邻接补墙或将顶端两行整行写成W/w，否则会重新出现黑色角洞及隔墙顶端断开。门洞和可走地板与实际游戏调查位置一同验收。

四宿舍南侧第9行为墙顶，第10—11行为连续W/w立面；四个门洞各宽2格、贯穿第9—11行共3格。主走廊净地面为第12—13行（64px）。研习室与备餐间南侧在第26—27行向外补W/w墙面，保留中央门厅通道。行号从0计。房门由RoomDetails放置在y=384，与门洞、门框和碰撞一同维护，不能只恢复旧门位置而保留新墙面。
