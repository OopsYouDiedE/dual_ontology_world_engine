# Native DV UI contract — 2026-10-19

本轮仅修改 language_core/game/entry.dv、language_core/verification/session/30-runner.dv，并新增本报告。未读取设计文档，未构建、运行探针、测试或正式会话。公共接口文档与 AGENTS 同步由主代理整合。

## 已实现

- `dvg_host_open(options)`：options 为 `{controller: "player"|"ai", viewer: actor-id, campaign: {...}}`，默认 player/detective/空配置。campaign 原样交给 `dvg_open_config`，不修改预算或章节条件。校验controller、viewer存在、cast、本人projection binding、真实位置及地图绑定。player模式调用 `dvs_set_player(viewer)`，ai模式调用 `dvs_set_player("")`；story.host_viewer存查看者。`dvg_host_viewer(state)`读取该值，兼容旧状态默认detective。
- `dvg_cast()`提供唯一角色外观表，snapshot与people共用；presentation.player_sprite取当前viewer对应值。
- snapshot和people输出真实逻辑位置对应的map、room、execution_room、logical_location、spawn及有则anchor。缺少当前映射不再伪装成侦探卧室。此空间投影仍仅供几何呈现，不写入人物认识。
- intent移除foyer→living_hall的旧lounge站点/房间伪映射；execution_room始终来自dvg_place.room，logical_location保留原travel.to，显式station保持原声明。非move但带显式travel.station也获得destination，防止未来现场动作接线只认kind漏掉站点。
- `dvs_set_player`允许pending缺省/false，真实pending时拒绝；非空actor必须存在，空串表示全AI。
- `dvs_control_state`改为 `dvs_owned_state_update`，只更新actors/events/cursor及控制字段，沿正式会话外壳合同保留pending、未知状态等字段。

## 房间映射依据

CampusMapView.gd:365–384 的 room_of 使用 map:sight_region；寝区对zone_yi/zone_ding先按 `x=35-cell.x` 镜像，再按north/south及floor(x/7)命名。原zone_yi:R05等不是该适配器实际回执房间。

rooms.py:83 的乙区numbers为 `[05,06,08,07]`；zone构造按x0=8、15依序取北/南房号（x0=1为空房），随后mirror。故R05=north_1，R06=south_1，R08=north_2，R07=south_2。zone_yi.tscn实际Sites/bed=(858,196)，换算cell=(26,6)，镜像x=9，对应north_1，独立支持R05映射。R05提供anchor=site:bed；bed不是Spawns，不再冒充spawn。foyer用真实Spawns/from_hall=(64,400)。hall移除不存在的table spawn。pantry依生成器备餐间范围与sight_region映射为prep。

living_hall:hall及bath、archive:works、resonance:hall/silent、clinic:ward/safety、logistics:dining等沿现有适配器实际区域命名；living_hall的east_south/west_north/west_south/east_north确为Spawns，campus_s的oath_court确为Sites。

## 尚未接线的几何（保持阻塞）

zone_yi.tscn 当前仅有Sites/transmitter、zone_rules、isolation_gate、bed；Spawns仅from_hall。旧E站点未在本批新增，没有用其他物体替代：

| Native动作 | 逻辑房间 | 现声明站点 | 当前execution_room |
| --- | --- | --- | --- |
| detective.touch_latch | foyer | site:E1 | zone_yi:foyer |
| actor.compare.E2 | foyer | site:E2 | zone_yi:foyer |
| actor.compare.E11 | R05 | site:E11 | zone_yi:north_1 |
| huo.deep-resonance | hall | site:E7 | zone_yi:hall |
| shen.deep-resonance | pantry | site:E10 | zone_yi:prep |
| su.deep-resonance | R08 | site:E6 | zone_yi:north_2 |

以上站点缺失必须返回blocked，不能据逻辑Complete或替代物件证明现场接触。outdoor_meter/outdoor_high/outdoor_alley等当前没有dvg_place地图绑定，本轮未猜测其地图；此为后续UI几何接线缺口。people中无地图绑定的主体仍不创建伪位置。宿主须使用room与可选anchor定位；spawn为空代表未声明具体spawn，不能把空spawn回退的位置当作逻辑房间真实到达。R05的床锚点是既有资源坐标，不代表通过床具碰撞验收。

## 验证范围

仅逐段源码/差异审读、现有地图生成器与tscn/适配器对照。未执行任何验收级别；未验证C#生成符号、HostOpen调用、Godot初始放置、遮挡、碰撞、输入、移动回执或画面。未修改剧情、成本、资格或正确条件。

末次源码SHA-256：

- entry.dv：1EDC05C70766ACD752931A4DD9F3C0D53E7615CA19F71D8DBF1B19AF756F19CC
- 30-runner.dv：6EE91C11C04E19C0211F0FA3324257422A8B5779AC7B939251DB165E6A607311

## 补丁：隔离门的真实地图权限

本补丁只再修改entry.dv。已读CampusMapView.apply_snapshot/can_enter/_refresh_access_geometry/_next_edge/_on_exit_entered与现役DV门法则，未构建或运行。

新增`dvg_map_access(state,current_map)`，snapshot.access.maps输出四个寝区和living_hall的布尔权限。乙区门读取原`c1dep_can_cross()`（outer_released=true、inner_down=false、open=true）；甲/丙/丁读取原case[group].outer_latch_released和campaign.access[group]，与原离区/交接law共有机械门条件一致。unknown按未开放处理，不注入开门。

当当前逻辑映射为某zone时，该zone的map权限始终true，保持出生/本区移动合法；其目的living_hall取该区实际开门值。当当前map为living_hall时，四zone各自转入权限取各自门态。因此乙区门不会通过禁止整个zone_yi来封锁床到门厅，封锁发生在出口目标living_hall；起居厅反向到zone_yi则按完整门态封锁。其他zone相同。

适配器会用同一maps过滤路线，并在每个出口目标处生成/启用CollisionShape2D屏障；snapshot刷新时重建导航网格。`_on_exit_entered`还只接受当前`_route_target_next`，并非任意走上门口就发起地图切换。上述为源码合同，不是碰撞或键盘实测。

`access.maps`仅能限制目的地图，不能表达同一门口的个人答辩回执、840时间、同伴陪同等主体条件。此补丁不将这些条件改成公共地图权限：本人动作资格、原enforce law及Complete依旧核验，地图开放不产生离区/章节交接。外观层也不能以换图替代位置的真实Complete。北/东连廊和室外通路没有在本补丁按猜测的地图边界重建权限；四区隔离屏障之外的几何门范围仍属后续明确接线范围。

## 初次物化的锚点核对

四个zone资源均有Stands，且只有Spawns/from_hall。zone_yi的Sites为transmitter、zone_rules、isolation_gate、bed，其他三区无bed站点。R05的`site:bed`与foyer的`from_hall`是现存明确绑定；R06/R07/R08、hall/corridor/pantry、其他三区的逻辑hall没有独立native anchor或spawn。任意Stands点或缺少Stands时靠玩家偏移的fallback，均不能证明该NPC位于其声明逻辑房间；from_hall也不能作为所有房间的出生替代。未新增猜测坐标或借用不对应物体。宿主若用room栅格筛选合法点，须单独核对实际房间和碰撞；若无此定位，则这些初次物化仍未接通。

本轮仍未构建、探针、测试或会话；root可在补丁静态整合后继续集中工程排错。补丁末次entry.dv SHA-256：BAB84EB51B9000BE821E54D2737A4968B5D0FC72A44102F4669D095EEBE4CD68。runner指纹与前节一致。
