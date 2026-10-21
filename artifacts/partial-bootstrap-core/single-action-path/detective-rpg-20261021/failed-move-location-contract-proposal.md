# 失败移动的实际停留位置：正式 Complete 合同方案（待取舍）

2026-10-21。只读工程源码与宿主代理核对结果；未读设计文件、未改生产源码、未运行构建或会话。本方案不提供剧情决定，也不把失败改写为原目标成功。

## 已确认的断裂

30-session.dv 的 dvs_session_complete 在现场拒绝时调用 dv_action_cost_finish(..., rejected)，费用/时钟正式提交，原目标法则不执行；因此 physical 中 actor.location_ref 仍是出发逻辑地点。宿主 DvFieldExecutor._execute_pending 却保留真实身体，并在下一动作要求 retained body 的资源/map 与 execution_snapshot 相符、departure_room(body.position) 与 source_room 相符。身体若已经跨房或跨图再失败，下一次仍收到旧源地点快照，可能不断返回 retained_body_source_mismatch/retained_body_room_mismatch。

不能用传送身体、改成 applied、删掉源核对、发正常 grants 或自动清拒绝签名解决。实际物理停留位置与“目标行动成功”需要在同一 Complete 中分别提交和记录。

宿主现有证据（host_audit复核）：外层 action_event/outcome/actual/executor=godot；field含who/serial/action/status/map/actual_position/transitions，_execute_pending另加source_map/source_room/source_geometry/source_position/actor_instance_id/execution_world。transitions只含跨图from_map/from_position/to_map/to_position/spawn/continuous；缺edge/exit标识，无同图room crossings；早期station失败的room常为空，现不能据room安全定位。source mismatch等前置失败未通过原始身份核对，不可作为位置更新来源。

## 推荐最小形状

在原正式动作上增加两个互相独立的部分：内容声明的空间执行合同，以及宿主从真实身体采集并核验的停留证明。通用30不出现map、房间或剧情名称。

### 内容侧：60在正式pending上绑定空间合同

优先在 dvg_prepare 得到一个新pending后、返回宿主前，将合同附在 pending.action.spatial_receipt_contract；随后 dvg_intent 只投影该冻结合同。不能只在dvg_intent返回的临时JSON中加入，因为Complete读取的是pending.action。

合同建议字段：
- version、contract_id、action_event（及会话/源码/内容绑定身份）；contract_id须与这次正式pending绑定，不能用跨动作静态字符串当本次授权。
- actor、location_ref、expected_source_logical；logical ref必须对应本次执行主体，不能任意写别人的字段。
- source与target的resource/map/execution_room/binding_id；本次允许使用的region、room边及portal/exit/spawn边。
- allowed_stops：stop_id、允许的resource/map/room或明确region、logical_location、对应权威position_law、位置反馈ref。只列本次执行路径确有映射的中间停留状态。
- terminal_goal另列原目标房间、station及误差要求；停留匹配不能替代terminal_goal成功。

空间映射不是dvg_place的简单逆字典：living_hall与threshold.*共用living_hall:hall；resonance与resonance_hall共用同一执行房；outdoor_meter与outdoor_high共用campus_n房间。故同一map+room不能取第一条或默认映射到目标。仍在源对应几何且没有越界证据时保留原logical；不同anchor需要声明可核验region才可区分。中间房间须显式列stop。不能用最近房间、目标名称、锚点距离猜逻辑地点。

### 宿主侧：统一收尾采集 actual_stop

只在源身份核对成功且已开始真实执行后，采集候选proof：
- version、contract_id/action_event、actor/action、执行serial；source_verified、execution_started；绑定实际source快照身份及本次执行起点。
- source:{resource,map,entered_room/departure_room,position,binding_id}。
- actual:{resource,map,entered_room,departure_room,position,binding_id}；来自收尾时真实body与当前adapter，而非destination。
- 同一body执行期间真实room_crossings，仅室内归属变化时追加；每条有from/to、房间判定依据、所用允许edge及position。跨图transitions补exit_id/edge_id/spawn和源/目标binding；continuous仅描述转交方式，不当作合法路径证书。
- last_verified_interior（含采集序号/所属空间及位置）；只记录实际进入过的室内归属。
- 分类：movement_blocked/contact_not_reached等原失败原因；位置验证状态和具体拒绝原因独立。

统一收尾必须先停止本次身体移动，并等待本动作已在途的 load_map 完成或取消到确定状态，再采 actual_stop、释放执行锁和交 Complete；不能在回执后让旧异步切图继续改变身体。起点不符、执行器不可用、缺绑定、没有真实执行的前置拒绝不得声称已移动。若执行中断但仍可取得活body，可同样统一收尾；body丢失/adapter销毁则保留原始.partial及错误，不猜终点。

## 怎样证明中间停留属于本次准许路径

宿主先按content合同对真实room_crossings/portal transitions逐边核对；30再次核对规范化证书与冻结pending合同：
1. 外层receipt属于本次action_event、executor=godot，actor/action/contract身份相同，费用时长仍与原声明一致。
2. 源快照、source_logical及location_ref与正式世界的本次执行起点一致；source_verified不是独立充分条件，原始source证据必须保存。
3. crossings从声明source连续连接到actual所在空间，所有room/region及edge/exit/spawn都在本次合同中；不能只要求终点在全局地图或“可达”。序列中断、非准许边、身份/空间binding不符必须拒绝位置提交。
4. actual_stop只能命中明确唯一allowed_stop。房间边界需要宿主原有entered_room/departure_room规则；D格若entered_room为空，只能依据已核验的departure归属或同一连贯路径中的last_verified_interior映射。仍有歧义则保留实际坐标证据、标记unmapped，不按最近房间/目标推断。
5. 同图同房且源/目标是不同逻辑anchor，若缺独立region证明，保留源逻辑名；这不会造成资源/房间source mismatch，也不产生目标到站事实。

这里验证的是同一正式执行产生的实际停留，不扩大发起动作资格。若真实路径越出允许集合，应明确报告执行偏离，不能靠扩大白名单事后合理化。没有可映射stop时仍可能无法继续，必须保留为具体未修复边界；不能用静默不更新来宣称软锁已解除。

## 同一个 Complete 内的提交次序

推荐使用现有权威法则求值器，而非裸dv_store改位置：dv_store本身不增长revision或追加正式执行历史（20-core.dv:221），独立使用会破坏版本与来源合同。

1. 保存原始host_completion。
2. 原目标按既有链判为rejected并且只结算一次原费用/已等待时间；action_execution保持失败与原quote/actual、原世界before/after。
3. 对已验证的actual_stop，调用该stop在内容中预声明并已enforce采用的position_law，由标准dv_execute在当前版本提交“源logical→实际停留logical”的唯一位置效果。法则仅写本主体location_ref，不含动作目标的其他效果；使用派生唯一事件标识，不能再次报价或扣费。position_law名称由合同选取，30不动态采纳任意回执法则。即使actual恰好在目标房而station失败，原目标依旧rejected。
4. 发独立spatial_position_reconciled事件，父项包含失败执行与原宿主回执；保存原始proof、验证结果、before/after版本和实际坐标。status仍rejected，不能把此事件加入completed_actions。
5. 再按实际位置与本轮已提交时间执行环境结算。实际停留可能触发既有位置条件，不能把环境依据继续留在旧源地点。若环境又产生真实变化，按既有规则分列事件。
6. 仅为本人产生已绑定location_ref的位置回执观察及既有时钟回执，经正常dv_perceive/dv_update_mind写入。位置回执必须引用空间提交事件，不复用action.grants；不发检视证据、答案、站点接触或他人状态。其他人物按既有真实共处观察链更新，不全员同步位置认识。
7. 维持used、失败预算扣减、尝试签名；不运行成功请求/对话/合作/完成分支，不继承目标成功planned_followup。

position_law的前提应只核对正式源地点及本合同源身份，不把未发生的目标成功或到站事实预写进去。预声明法则与合同只在内部位置结算分支可用，不能自动作为玩家可选动作或预测事实。精确数/坐标保留原值，来源不改写。

## 对现有机制的影响

失败重试：新位置回执是本人新的真实输入，故下一次拒绝签名/规划自然失效并重新核对；自主主体不是被人工“清签名”放开。原行动在实际中间房通常不再符合travel.from，本人只能选择从该停留点声明的合法动作。成功一次限制仍只看completed_actions；manual_contact_retries仍只释放人工显式接触重试，不能授权任意逆行或从不匹配源执行同一动作。若位置没有变化，保留自主相同签名保护。

排队：其他主体队列/余额不重置。当前动作费用与时长只结算一次，reconciliation是同次Complete的位置事实提交，不再占一个行动配额或额外时间。正式事件应区分原失败与位置更正，不能把额外法则执行误计为另一主动作。

扩章：新合同由60空间声明生成，章节扩展保留主体当前logical/knowledge/used/ledger/队列；不能按新章节默认出生点重建身体或重置位置。空间binding变更必须有版本，新的pending使用当前合同，旧pending不能在中途换表。合同不能重新暴露未观察地图或资源为本人知识。

存档：已完成动作存档保存更正后的正式位置、本人位置回执、原失败与空间事件、used/重试凭据；正常恢复按已有身体重建合同放在映射地点，不凭归档坐标跳关。原始坐标是证据，若要精确恢复脚点，必须另有原存档已验证的几何恢复合同，不从本方案自动推定授权。

旧存档：无pending的旧阶段存档可在下次Prepare生成新合同；不能追溯注入历史位置或删除失败。若旧存档的retained body与正式logical已不一致，先明确诊断；不能把新合同用于补认过去未留证据的位置。在途旧pending缺合同/版本则不能合成一份新授权套用，保留具体恢复错误或由主代理依既有恢复合同决定。源码/配置/提交指纹仍按现行恢复要求核验。

## 待主代理取舍

A. 推荐完整修复：新增显式allowed stops/room-edge/portal声明与宿主真实crossings证明，再接标准位置法则与本人回执。能覆盖已映射的中间房和跨图失败，工程范围较大。

B. 较小第一阶段：只接受source/target已明确且唯一映射、宿主能证明路径合法的停留；同房保留原logical，缺中间stop或歧义时明确unmapped。不能称所有失败移动软锁已解决。

两者都不得事后扩大路径、以失败当完成、预置目标观察、自动提高预算或跳过真实碰撞。此方案尚未实施，未验证视觉、行为、性能或存档结果。
