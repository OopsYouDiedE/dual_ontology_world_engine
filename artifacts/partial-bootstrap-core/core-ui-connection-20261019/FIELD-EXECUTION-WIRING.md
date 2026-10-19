# 现场执行接线：源码审读记录

日期：2026-10-19。范围：WorldView.gd、CampusMapView.gd、新增 Host/DvFieldExecutor.gd。Main、DV、C# 与工程合同由主代理及对应代理整合。本记录为源码静读，不是运行证据。

## 修改结果

`DvFieldExecutor.execute(intent, visible_field, execution_snapshot, serial)` 等待真实 `daily` 回执。相同可见地图的已有身体使用现有 WorldView 路径跟随器与 CharacterBody2D 碰撞。远端主体及需要跨图的非玩家主体，使用独立 SubViewport / World2D 内的同一 WorldView、地图和跟随器；隔离现场不连接 UI，也不生成其他主体副本。现有 collision_mask=1 保持原值。新物化身体仍按真实可见玩家130、其余主体110的现有速度规则设置，转移身体保持原速度。

主体注册表以 DV actor 为唯一身份，保存同一身体实例、实际地图、资源与所属现场。可见现场被移除前 retain_field 将身体转入独立停放世界，关闭其处理；configure_visible / bind_visible 和 acquire_actor 转交同一身体及原地图坐标。已执行身体不从快照重新定位、不复制、不按散列站位重建。新正式会话 reset 清空注册表；关闭调用 begin_shutdown。

首次物化从执行快照或 people 条目的 execution_room、map、anchor、spawn 与 initial_placements 读取绑定，只采用原地图既有 anchor、Stand 或 Spawn，并核对真实 entered_room 与 walkable。显式 anchor 优先读取；不存在、真实房间不符或不可步行时返回 blocked，不以其他站位替代。无显式 anchor 时保留原有同房间合法 Stand/Spawn 选择。已有身体的实际地图与源 execution_room 必须符合本轮执行快照；缺少映射、身体资源、真实房间或资源绑定时返回 blocked。其他主体的私有快照不应用到玩家 UI。

跨图沿真实源图路径到达场景出口或室外边界；仅当前准备动作的下一条地图边允许出口回调换图。原身体停掉旧图路径，经过声明的目标 Spawn 或室外连续坐标变换交接，再在目标图继续真实移动。声明缺失、目标静态几何不合法或区域坐标不连续时阻断。首次物化与普通快照刷新不能自行触发跨图。

CampusMapView 保留 departure_station 与 execution_room；site: 站点使用既有热点的 point / reach。WorldView 回执保留主体、序号、行动、实际地图、真实房间、起终点、身体实例、跨图变换、站点、站点范围与到达误差。到达后释放日常动作占位，避免完成记录继续占据后续站点。

## 逐文件静读事实

- WorldView 的原 create_body 在返回前已加入 objects_root；acquire_actor 仍对原父节点为空提供保护。转交只换父节点并恢复原地图局部坐标。
- 停放身体位于独立 World2D，不留在可见碰撞世界。隐藏执行身体重新加入现场后，先等待物理帧，再启动原跟随器，令身体与延后更新的门禁碰撞进入物理世界。
- 隐藏现场关闭视线绘制、悬停与 UI 处理；保留现场物理处理与原移动跟随。空玩家现场关闭视线处理，context 返回明确不可执行的空主体上下文。
- WorldView 无自由键盘或鼠标移动处理；正式移动由 execute_move 接收准备意图。
- 成功回执在执行器校验当前主体、序号、行动、地图和真实房间；显式站点还校验 station 与 arrival_error。Main 负责统一接收、再次校验并 Complete。

## 尚未验证

本分工未启动构建、Godot 语法排错、探针、逻辑会话、游戏、截图或任何 flow。目标地图的作者 Spawn 是否均满足实际几何，完整跨图路径能否执行、动态门禁物理同步、可见与离屏身体交接画面、全部主体资源绑定及 UI 体验均需主代理后续统一排错及获授权的实际会话核对。本记录不宣称任何验收级别完成或游戏行为改善。
