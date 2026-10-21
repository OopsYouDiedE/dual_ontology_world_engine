# blind-03连续过门取消后人工重试封禁：只读诊断与最小方案

2026-10-21。只读blind-03原始二进制证据与其冻结运行副本tmp/detective-rpg-final-20261021，未读设计、未改源/构建/运行，未触碰candidate04冻结副本。冻结30-session.dv、Main.gd、WorldView.gd的SHA-256均与blind-03/source-before.json相符。

## 结论与实际证据

确认这是一次已经正式提交的移动失败，随后同一移动从人工候选中消失；不能继续解释成“未真正触发过门”。取消本身按现行输入协议合法，封住后续显式重试是另一个机制问题。

inputs.dvlog保存continuous_walk requested：choice_id=detective.move.R05.corridor，request_id=spatial-162504729-5，actor=detective，map=living_floor，from_room=zone_yi:north_1，to_room=zone_yi:corridor。随后action31得到真实Complete：顶层executor=godot、outcome=rejected、actual.time=2；field为kind=daily、who=detective、action=detective.move.R05.corridor、serial=32、status=blocked、reason=released、continuous_walk=true、同一request_id。

field.source_map与map均living_floor，source_room与room均zone_yi:north_1，source_geometry=source-side-doorway，transitions=[]；source_position与actual_position精确相同，均[2401.66455078125,991.000854492188]；prepared_to_receipt_usec=132。这支持本次Prepared时已取消，未在该次正式执行起点后继续移动，并在源侧安全结算；它不支持到达corridor或获得目标观察。

DAG：侦探e193选择该移动，e352在游戏483分钟action_execution=rejected；最终只有醒来和此移动两项侦探选择。final actor.location=R05，used含该移动，completed_actions不含，manual_contact_retries[该id]=false。rejected_attempt_signatures保存本人location=R05、同源预测法则及其前提，opportunity=false；没有时钟、局部坐标或按键状态。

host-events.dvlog更直接证明候选变化：31941ms的state/dv_action.awaiting_input含detective.move.R05.corridor；175293ms的state/dv_action.awaiting_input只余identify-own-lens、wear-coat、inspect.E11、inspect.E12，该移动已不在其中。

## 源码原因

冻结WorldView.gd:1627的_finish_cancel_if_safe仅在prepared、cancelled且departure_room等于原from_room时发送blocked回执；松开输入沿_stop_walk(released)及_cancel_continuous进入该路径。若还在边界中，它先真实碰撞返回源侧，不能传送或伪造到达。

冻结30-session.dv:1684的dvs_manual_contact_failure不覆盖此合法取消：
- 第一分支要求field.executor=dv-world且contact=false，此回执是continuous daily，不符合。
- 第二分支明确要求reason为空；released在这里立即被拒。
- 即使只去掉reason限制，后面还要求station/approach_point/station_point/station_radius/arrival_tolerance/arrival_error等完整旧job证明，而continuous回执不具备这些字段。

因此Complete记录拒绝签名，却没有人工重试token。eligible_base:1708以后在manual_input视图中也因同签名返回false。局部走位、松开再按和游戏钟变化都不改变本例签名；若没有其他真实相关本人输入使签名变化，该移动将持续被封禁。严格地说它是“对当前本人输入永久”，不是任何可能未来世界状态都绝无恢复；本轮证据已实际显示未恢复的菜单。

## 最小安全修正方案（未实施）

不要全局忽略released，不删除拒绝签名，不取消失败费用，不把取消记成成功。为真实人工连续过门的安全源侧取消增加独立分类manual-continuous-source-cancel，仅发同action id的显式人工重试控制token。自主主体依旧同签名不自动重试；所有正式资格/门禁/费用/执行继续重核，used与失败证据保留，completed_actions/grants/答案不变。

可先覆盖本例的严格“本次正式执行未位移”子集，不需要把整个失败位置同步方案一并实现：
1. Complete仍核对顶层action_event属于pending，actor为当前player_actor，executor=godot、outcome=rejected；action.kind=move且travel.from/to是明确不同地点。
2. 宿主保留原请求与Prepared绑定，核对field.who/action/serial/request_id分别与该次Prepared主体、动作、执行序号及continuous_request_id一致。现有Main._verified_continuous_action已检查请求与独立DV intent，但重试分类仍须核对实际返回字段，不能只看continuous_walk=true。
3. 60或正式pending合同提供本次source/target的显式map/execution_room绑定；30使用声明核对field.source_map/source_room、field.map/room，不在通用session硬编码living_floor或房间名。若现有DV pending缺此绑定，可由Main在完整身份核验后添加规范化source-cancel证明，但必须保存原始字段与对应pending绑定，不能由任意回执自报一个verified=true即放行。
4. 只接受reason=released的明确人工取消，kind=daily、status=blocked、continuous_walk=true；身份、绑定、执行器错误或其他reason不走此分类。
5. source_map=actual map=声明源map，source_room=actual departure room=声明源room；transitions必须为空；source_geometry仅可为经过现役departure_room核对的interior或source-side-doorway。源门槛D格允许entered_room为空，但departure归属必须明确仍在原源房，不能用目标/最近房间猜测。
6. source_position与actual_position都是两个有限数且精确相同，禁止用新的容差把真实移动压成未移动；宿主body identity与实际执行源核验成功。本轮原始回执满足此坐标相等条件，且132微秒收尾符合已取消后才Prepared的时序。
7. 在原Complete末尾写现有manual_contact_retries[action.id]={failure_class,action_event,...必要请求绑定}，后续同动作Complete覆盖/成功清除；只由manual_input读取供下一次真实人工选择，不自动重发、不断循环或把取消当事实。

更广的“中途取消并真实返回源侧”不能只用source_position!=actual_position加reason=released推定安全；应另保存同一允许源/目标边界内的真实轨迹/room crossings、碰撞返回终点及已停止的异步执行证明。源/终点一致、transitions=[]只能证明无跨图与当前源归属，不能独自证明中途没有越过其他同图边界。先修严格无位移子集应明确未覆盖此类返回。

## 合同与验证边界

落地时同步continuous门动作及人工重试合同；新失败分类不进入mind或公共观察，存档按现有actor重试字段保存。旧存档中本例false token不能凭空追认；若要对已封存历史失败恢复重试，必须从完整真实receipt及相同绑定重新验证，不能把任意旧拒绝一律清掉。

未来必要实际复核分别观察：合法按键释放仍失败/收费；再次真实按门输入后同动作重新出现并独立Prepare；第二次真实到达才Complete成功；未到目标无观察；自主主体不因released自动重试；串行旧serial/request_id或错误源绑定不能生成token。本报告未运行这些验证，不声称candidate04已修复或通过。