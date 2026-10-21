# 失败移动真实终点：宿主回执最小修补方案

状态：2026-10-21，仅源码审读与接口方案；不改源、不构建、不运行。与 core_audit 协商；本文件不读取或解释设计。目标是在同一个 Complete 保持目标动作 rejected，同时正式记录已真实发生的身体位置，避免下一次源身份核对永久失败；不传送、不冒充成功、不回滚认识。

## 当前证据与缺口

| 信息 | 当前来源 | 现状与缺口 |
| --- | --- | --- |
| 正式行动身份 | Main._dv_complete 外层 action_event；field who/serial/action | outer action_event 已唯一关联 pending；field 缺与其直接绑定的同值，serial 目前只是宿主相关号，DV 未曾持有该序号时不能声称已独立核对它 |
| 源身体 | DvFieldExecutor._execute_pending 源身份核对后捕获 source_map/source_room/source_position/source_geometry/actor_instance_id | 来源地图、房间来自执行快照并与真实身体核对，但 source_resource、执行空间绑定版本/指纹未显式写出 |
| 实际终点 | WorldView._daily_report 的 map/actual_position | body 存在时即使失败也有实际坐标；room 是调用者参数，缺站点/无路径等早退传空，不能作为统一真实房间证据 |
| 跨图转交 | WorldView.load_map 在同一身体真实转交后追加 transitions | from_map/from_position/to_map/to_position/spawn/continuous；没有出口ID、所用声明路由边、出入口房间、进入时通行值及绑定版本 |
| 同图跨房 | entered_room(body.position) | 当前不记录跨房顺序；停在门槛 D 时 entered_room 为空，仅以 room_of 或目标房间补值会猜错最后合法归属 |
| 前置拒绝 | DvFieldExecutor._blocked | 可含当前保留身体map/actual_position，但未经过本轮源身份验证；不能把这些值当成新正式物理运动的可信终点 |
| 停止后静止 | _daily_report 调 _daily_release | 实际坐标目前在取消路线前读取，通常不变；方案应先停止该身体、确认无在途切图，再捕获稳定终点，避免回执后还有旧异步回调转交地图 |

## 最小生产改动范围

1. `DvFieldExecutor.execute` 每次接受 pending 后建立仅本次有效的执行上下文：action_event、who、serial、action、source_resource/map/execution_room、绑定标识、body instance、source_position。只有源图、源资源、源房核对全部通过，才置 `source_verified=true`；只有现场分派真正开始才置 `execution_started=true`。不要在 `_blocked` 通用前置失败路径自动补这两个真值。
2. `CampusMapView.execute_destination` 和 `WorldView` 使用同一上下文记录真实转交。已有跨图 transitions 继续保留并补声明 edge/exit ID、from/to execution_room、相应绑定标识；记录必须在实际转交完成后追加，不能把计划边写成已通过边。连续室外 seam 保留坐标连续证据。跨图链从 source_map 连续到 actual_map，不跳边、不虚构中间图。
3. 同图执行期间记录有限的 `room_crossings`：实际身体从已确认室内房间进入另一个已确认室内房间才追加，包含前后地图/房间、实际边界前后脚点和物理采样序号；没有变化不逐帧复制。跨图后同一跟踪继续。初始 `last_confirmed_room` 来自源身份核对。不是用预测路线、destination、最近站位或地理距离推断。
4. 所有已经开始执行的返回点统一进入终点收集器。先停下本次身体并等待/取消本次在途转交落定，再从同一 retained body 所属的实际 field/adapter 读取 resource/map、actual_position、entered_room、departure_room、最后确认室内房间、实际转交链、停止原因及 body identity。不得采样计划目标或旧 snapshot。若身体/地图已释放或异步转交无法落定，保留 `endpoint_valid=false` 和错误，不能据猜测更新DV地点。
5. `Main._dv_complete` 保持 outcome=rejected，额外传结构化 physical receipt。非移动且没有 travel 的一般拒绝动作不进入位置同步。普通 move 和有 travel 的动作可以产生失败位置回执，但目标接触/inspect/grants/答案/完成效果均继续 rejected；成功站点的原半径、房间与到达误差校验不变。
6. DV 同次 Complete 依 core_audit 的通用校验合同安装已验证的物理地点；Complete 后现有 state snapshot 使用该地点，下一步沿原 view_ready/dv_next 准备。宿主仍保留相同 body 和真实终点，不要再为“同步”重生或改坐标。前置 source mismatch 不开启该通道；它是故障，不是可伪造的事后移动证明。

## 建议回执形状

以下为接口草案，不是运行轨迹：

```text
receipt.action_event = existing pending identity
receipt.outcome = rejected
receipt.field.who / serial / action = existing identity
receipt.field.physical = {
  version: 1,
  source_verified: true,
  execution_started: true,
  endpoint_valid: true,
  action_event, actor_instance_id,
  source: {resource, map, execution_room, position, geometry, binding_id},
  actual: {resource, map, entered_room, departure_room, last_confirmed_room,
           position, geometry, binding_id, stopped: true},
  transitions: [actual completed authored exit or continuous-seam transfers],
  room_crossings: [actual confirmed room changes],
  stop_reason
}
```

不要求同时保留冗余结构永远两份；实施时可保留旧 field 字段兼容并指定新结构为位置同步唯一入口。全部进入现行二进制原始证据，不另存 JSON 轨迹。body instance 只保证同一进程本次执行的对象连续性，不用作跨存档身份。

## DV 可核对与不能核对的范围

内容向 generic session 提供明确的执行空间映射：resource + map + execution_room 对应可继续行动的 logical location，并声明允许的路由边/出口身份。宿主只回传空间身份，不自行挑剧情地点。若一个房间映射多个逻辑地点，必须由内容显式指定“仅物理在场”的唯一恢复地点，不能用动作目标、站点接近或字符串拼接猜。绑定应在 Prepare 时冻结/标识；版本不符是拒绝位置更新的具体错误。

Session 核对 pending subject/action/action_event/source、执行器类型、结构版本、binding、实际空间映射、跨图链连续性、声明边身份、终点与最后转交图一致；有同图跨房则核对房间改变链来源和最后房间。`serial` 只有在 pending 曾明确绑定预期宿主执行号时才能比较；否则仅作为相关证据。指纹、body instance 与采样号不会证明碰撞正确；Session 不拥有Godot几何，不能声称重算过物理轨迹。实际碰撞与脚点真实性仍由生产Godot执行器及独立新运行证据负责。

终点为合法室内脚点时使用实际 entered_room 映射。终点停在 D 或 seam 时，保留真实坐标和最后确认室内房间；只有声明映射及已记录合法链能唯一确定可恢复地点才安装。不能以D格 room_of、空room、目标room替代真实来源。若现存合同不能表达该合法门槛状态，应明确扩展“边界脚点的逻辑归属”合同；不调整身体位置。

## 目标效果与位置观察

同一 Complete 可返回 `status=rejected` 与独立 `physical_position=updated|unchanged|invalid`。位置更新只说明本人真实停在何处；允许本人收到该位置回执。不能调用一般成功移动/接触观察入口来顺便揭示房间证物、人物、正文、谜底或给物品，也不能把“已到中间房”记成原目标动作完成。其他主体只沿已有真实可感知传播法则处理，不能全局广播未知位置。费用按现有单次结算合同处理，不因位置子提交重复扣费或额外推进时间。

## 必要独立新运行验收

- 同图真实进入中间房后受阻：目标仍rejected，真实终点不变，下一个从实际映射地点准入的动作可执行。
- 已真实跨图后最终anchor/station失败：同一身体、实际图与DV新地点一致；目标成功观察/inspect/grants/答案均没有产生。
- 失败停在门槛：没有猜房间、没有传送，可继续合法移动；如果绑定不能唯一表达，保留具体错误。
- 伪造/错配subject、action_event、source、binding、边、终点图或未开始执行的失败：不更新地点、不绕过成功条件。
- 重复Complete：原有行动去重保证位置、费用、本人位置观察只提交一次。

此方案修复未来完整证据链中的失败；不能自动修复已被旧Complete拒绝、且缺原真实路径证据的历史失配状态。此前静态preflight方案可以保留降低失败概率，但不能替代上述合同。
