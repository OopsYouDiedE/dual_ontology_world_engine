# 一倍速度与卧室门槛出发修复

用户反馈：初始卧室无法进入走廊，要求正常游玩改为一倍速。

依据用户实际会话20261019-033149-3506：门口输入已提交detective.move.R05.corridor，action17的实际起点为[811.743591308594,287]，执行器因retained_body_room_mismatch拒绝。原回执逐字节摘录见original-failure.jsonl。此处是可走D门道，entered_room有意不把它当成已进入房间；此前起点检查却复用这个到达谓词，导致合法门槛出发被拒绝，随后动作被原重复失败资格屏蔽。

修复：CampusMapView新增departure_room，允许源几何分区内可走D格作为出发点；DvFieldExecutor仅在源核对采用此接口，并在回执记录source_geometry。到达仍使用原entered_room、主体/序号/动作/房间和真实终点证据，没有放宽目标到达、开放墙体、预置DV状态或清除历史失败。鼠标点门和近门E从实际连通门道匹配现有准入动作，真实走到门口再提交；多义门不猜选。正常Main默认playback_speed=1，新窗口也显式使用--dv-speed=1；独立采集的显式倍率没有修改。

工程记录：完成Godot资源导入。geometry_check.gd仅加载现役地图/脚本并查询几何，没有打开DV会话、执行玩家路线或注入状态。geometry.log记录原失败坐标departure_room=zone_yi:north_1且entered_room为空，走廊实际地板点[811.743591308594,360]的entered_room=zone_yi:corridor；墙点[704,280]不可走且无出发/到达房间。该工程排错不证明整段实机移动已达成。

已重新启动普通可见游戏窗口，供用户新会话体验和反馈；没有自动游玩、流程回归或完整游戏验收。既有失败证据与project.godot原有修改保留。新窗口运行日志留在本目录game.log，运行时不将其封存为完整会话证据。
