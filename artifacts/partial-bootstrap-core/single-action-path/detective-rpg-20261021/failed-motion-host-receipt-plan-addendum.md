# 失败移动宿主方案补充：与 core_audit 对齐

本补充优先于初稿中的静态全局映射措辞。仅方案，无生产修改、构建或运行。

执行 map+room 到逻辑地点不是一般可逆函数；同一房间可能存在多个阈值、锚点或逻辑地点，不能遍历映射取首项。每次 pending 应由内容层明确提供 `allowed_stop` entries、`contract_id`、允许房间/边/portal 白名单；generic session 只校验该次合同。若真实终点仍满足 source geometry，优先保持 source logical location。只有 allowed_stop 及实测 entered/departure/last_entered、room_crossings、transitions 能唯一解释真实终点时才转到对应 logical location；目标与源同房但锚点不同且没有可核实region时，不能推断已到target。

宿主补充最小proof：version、action_event、actor、action、serial、contract_id、source_verified、execution_started、source(resource/map/room/position/binding)、actual(resource/map/entered_room/departure_room/position/binding)、room_crossings、transitions、last_entered_room。serial仅在pending约定后核对，不把“数值非空”冒充身份验证。

跨图记录必须补实际通过的edge/exit_id/spawn，不能以continuous标记代替已走路径证书。它只表示转交前后保留同一身体/连续脚点。门槛 entered_room 为空时，允许来源只能是已验证的departure_room或最后已证室内房间；不能最近邻猜房。若合同无法唯一映射，保留真实身体、未映射错误和原始回执，不能传送或宣布原目标成功。完整方案必须覆盖常见合法门槛端点，避免仅把softlock改名为错误。

在具体实现前，由主代理核对内容层allowed_stop是否完整以及本人位置回执的展示来源；不由宿主补设地点、剧情或观察。目标action仍rejected，成功接触/检视/grants/答案/目标完成效果保持拒绝。
