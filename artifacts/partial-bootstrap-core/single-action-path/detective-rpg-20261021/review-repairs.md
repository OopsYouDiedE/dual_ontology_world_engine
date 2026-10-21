# 本轮修复独立工程审读

审读日期：2026-10-21。分支：codex/detective-rpg-20261021。

结论：本次静态审读未发现可确认、可复现且应列为 P1/P2 的新增工程缺陷。此结论只覆盖以下源码与接口核对，不表示游戏体验、性能、画面或剧情验收完成。

## 范围与方式

已读取根、language_core、godot_csharp 的适用 AGENTS；未读取设计文件。以本任务 baseline 中对应文件为本轮差异基准，检查当前 30-session.dv、40-chapter1.dv、60-game.dv、Session/Session.cs、WorldView.gd、InvestigationUI.gd、UISystem.gd，并沿 Main.gd、CampusMapView.gd、FieldHUD.gd、20-core.dv 核对实际消费者及来源。未修改源文件，未运行构建、语法探针、正式会话、游戏或流程回归。报告之外仅执行只读源码/Git 查询。

## 已核对的工程边界

- 成功与尝试分离：资格中的默认不重复/once 门改读 completed_actions；Complete 仅在 applied 时追加 completed_actions，used 与费用/行动配额仍记录失败尝试。接触重试只影响相同拒绝签名，不放宽正式前提和成功限制。
- 人工接触失败：重试凭据只在 player_actor 的非 applied Complete 写入，要求 godot rejected；只接纳非 self 热点的明确 contact=false，或身份/行动匹配、无错误 reason 且具有完整运动 job 字段的 daily blocked。缺资源/缺绑定等无完整 job 失败不生成凭据；formal-model 不生成；成功或后继不合格失败覆盖清除。同一凭据只有临时 manual_input 视图使用，自主资格仍受拒绝签名约束。
- 自主规划：Prepare 移除强制 plan_depth=1/fast_approximation/lookahead_limit=4/plan_budget=32 的整批覆盖；每轮仍以既有 planning_limit 更新 plan_budget，idle 固定点路径保留。本结论不外推运行耗时或规划质量。
- 近门 E：_near_door 汇总真实 D 格相邻房间与 armed/can_enter 的 Exits/Shape 矩形；与当前 available doorway options 匹配并按行动 ID 去重，总候选仅一个才返回。正式提交继续经过 can_submit 与 Prepare/Complete，几何查询不授予到达。
- 热点/低动态：HintOverlay 的每项绘制调用既有 site_visible；reduce_motion 时停止 clock 增长并采用静态火花半径。视野遮挡、实际截图和设置切换效果仍须 UI 检查。
- 地点 HUD：新增 room 名只取当前 viewer 的 position 对应 logical_location；未新增读取远处人物位置来补地点名的路径。权威地点与实体部分失败后可能不一致的既有缺口仍由宿主 AGENTS 明确保留，本轮未据此声称已修复。
- 可读重演比较：读取本人的已保存 simulations；逐项只接纳内容白名单精确 feature、单个对应观察 cell、与本人当前 cell 完全一致、正置信且无争议的结果，并要求本人 seen 中存在相符 perception claim 与行动来源。无 world 读取、模型执行或预测写入本人认识；旧/缺元数据记录保守不公开逐项比较。
- 人工假设输入：UI 保留候选 row.id，通过 JSON envelope 传递 action_id/hypothesis_id；UISystem、WorldView 按底层当前行动 ID 重核并保留完整 envelope。Session.ParseValue 进入 SelectPlayer，player_choice 把临时 replay_hypothesis_id 放入 choice.action，scheduled_action 与 pending.action 均保留该字段，不改永久行动定义。
- 人工重演隔离：候选需经共同 dvs_manual_candidate_available 的 topic/basis/available_when/manual_available_when 校验，SelectPlayer 与选择及 Complete 均重核；人工只模拟选中候选，attempt key 为 action/candidate 组合，签名含 hypothesis_id。未选假设不能走旧的全候选人工提交。
- V6 工程一致性：变量投影、具名 V6 候选、组合中 familiar=true 的服务端门使用相同 c1_manual_history_gate。门未开时组合投影隐去 V6，服务器仅允许 familiar=false 的既有组合，并明确交付“暂按彼此陌生推演，尚未查明”的假设说明；无新增实际熟悉度事实读出。

## 未覆盖范围

没有运行时证据，因此不确认 Godot 控件布局、闭包实际交互、语言切换、碰撞路径、异步等待、现场失败复试体验、模型预算消耗或重演结果是否在实际游玩中可达。也未审阅无关音乐、美术、设计文档或其他任务改动。若后续修改本次审读的代码，修改部分须重新审读。
## 追加审读：P1 人工单选重演切断正式解释所需的竞争者覆盖

本节修正前文“未发现 P1/P2”的结论：对正式解释链补查后确认一项 P1。定位：30-session.dv:6346、6356、6361 的 manual_id 分流，以及 5110 起的 dvs_arguments / 265 起的 dvs_ready；20-core.dv 的 dv_accept_explanation。

触发条件：新开的人工会话，本人尚无既有 factor_certificates，原问题的 six-variable-history 同组候选已开放；本人付费选择一个完整且与全部已得观察相容的历史组合。人工 Complete 将 candidates 限为所选项，同时因 manual_id != 空跳过 dvs_factor_prune、覆盖表与 factor_covered 路径。下一次 manual_replay_refresh 会走正式 dvs_arguments，但全部未覆盖的同组其他候选仍被当作 competitors；它们没有模拟记录时，dv_accept_explanation 以 unresolved-competitor / competitor-not-refuted 拒绝建立正式解释。dvs_ready 同样报告 unresolved-competitor。UI 中“相容”不能转化为正式作答资格。

这不是仅多做几次重演可以解决的问题：c1_hist_domains 有 3×3×2×2×3×2 = 216 个组合，原 model_budget 总余额为 320，且每次付费重演仅重设 model_work_remaining，不补总余额。即使每个反例都只需一条预测规则与一次比较，215 个竞争者已需至少 430 单位，更不包含所选模型、假设、响应及其他模型消耗。实际反例切片也不能以零成本产生完整矛盾记录。因此无已有可复用证书的新人工路径无法靠逐个选择完成竞争者排除。这会阻断依赖该正式解释的作答/后续推进。

最小修复建议：保留人工只运行选中模型的 candidates=[selected]，但在同一次已有 paid_model_event 下，对人工与自主都运行既有 dvs_factor_prune，沿原 policy.model_budget / model_work_remaining 扣费并保存带 prune.event 的 factor_certificates；覆盖表仍走原 dvs_factor_coverage / certificate_current / catalog_complete 校验。可按证书记录被排除的候选 ID 以供正常拒绝判定，但不因此调用任何其他候选的模型。不要把 UI 选中或“相容”直接作为证书、无需删减正式 competitors、不得增加 work_limit 或补回 model_budget。若来源或余量不足，原样保留未完成/未知。

单选模型的 work_limit 判断：原 replay 的单次 work_limit=320，对单个完整历史模型本身足够；瓶颈是竞争者证明被删掉，不是必须提高单次配额。静态计费核对中，完整时间线至多约 59 个 plan 条目、9 个 assumptions、4 个 hearing dispatch（每个最坏 6 条，额外至多24）、一次四 context/三 option 响应共10及桥接1、7次特征比较，完整模型约110单位以内；dvs_model 的先行因果切片最多再执行一次不大于完整 plan/assumptions 的子模型，合计保守小于220。此为当前声明与计费源码上的上界审读，不是正式运行测量。Replay_Depth 仍须覆盖时间线，model_budget 剩余额度仍须足够。恢复约束剪枝后，证明开销也须从同一320与总余额扣除；具体主体当前的证据链长度/可用证书能否连同所选模型在剩余额度内完成，未执行会话，不能宣布通过。最小修复不应掩盖预算耗尽。

追加审读仍未读取设计文件、未修改源文件、未运行构建或会话。
## 最终合并静态复核

已追加审读 perf-integration-01/integration-report.md 所述合并后的 Main、WorldView、UISystem、TitleUI、Shared/Input/UIInput、ModalPanel、GameEngine 与有关工程合同；仍为只读源码审读，无构建或运行。

前述人工剪枝 P1 的具体代码缺口已修复：30-session.dv 的付费 replay 对人工与自主共同执行 dvs_factor_prune，按原总余额/单次余量扣费；coverage 表与 factor_covered 恢复。人工 candidates 仍严格为所选一个，未重新批量执行竞争模型。Session README 与 language_core AGENTS 已说明共同付费剪枝、单模型与来源覆盖边界。此结论确认源码接线修复，不代表已实际建立解释或通过正式作答。

人工 action_id/hypothesis_id 封装链在合并后仍保留；新增 _can_select 核对当前 snapshot.pending.options 中原 action ID，_submit 保留完整 envelope 交给 Session；新增门道输入没有把人工重演 JSON 改为只有 action_id。可见记录同步扫描共用 context 并在添加字段前复制，控制激活上下文按每次扫描更新；手柄原始事件仍单独记录。未发现因此新增的隐藏世界状态读取。

同图 continuous 请求从真实源侧门道与当前行动选项发起，Main 再匹配 prepared 主体、action、地图、房间和 request；仅给该动作 destination 附加 continuous 模式。收到 prepared 后由同一 field.execute_move 安装等待，真正进入目标 interior 并通过 walkable 后只发一次 daily arrived；phase=arrived 防止再次回执，GameEngine pendingEvent 继续避免重复 Prepare。权威拒绝后，reconciling 路径使用碰撞移动真实回源，不写位置、不补 applied、不另提交正式选择或 Complete；受阻保留请求与 blocked 状态。

### P2：书签边界未覆盖连续过门的真实回源协调

定位：godot_csharp/scenes/Host/Main.gd:352 的 _bookmark_boundary_ready；WorldView.gd:1502 的 synchronize_continuous_snapshot 与 1542 的 _reconcile_rejected_arrival。

可复现的状态序列：连续过门已真实到达，但正式 Complete 因当前权威条件拒绝；新快照将身体请求置为 reconciling，DV 已无 pending，field_executor 已退出，_daily 也为空。回源路径仍在真实移动，或因占位/路径受阻保留 reconciliation_state=blocked。此时打开案卷请求保存/载入，现有宿主书签门只检查队列、Complete、adapter switching、执行器和 _daily，因此可认定边界已落定。_continuous 与回源路线不在世界存档中；载入清空原现场会丢弃未完成的真实回源协调，存档/现场边界结论亦不符合“现场动作结束后再保存”的合同。

建议在 _bookmark_boundary_ready 对 field.continuous_request() 非空保持未就绪，至少覆盖 arrived/reconciling 及未完成的 requested/prepared/settling；由真实提交或实际回源清除后恢复原排队保存，不因书签请求强制清请求、传送或补成功。此项是新增连续状态与既有书签边界未接线的静态发现，未执行运行时复现。

UI/controller 导航、strict reveal 实际绘制、碰撞回源是否受动态门控影响及异步交错仍待冻结后验证。
## 最终书签门修复复核（以当前生产源码为准）

Main._bookmark_boundary_ready 现已在 field.adapter.switching 与 _daily 检查之间明确加入 `if not field.continuous_request().is_empty(): return false`。该门覆盖 requested、prepared、settling、arrived、reconciling 全部非空阶段。权威拒绝后的真实回源（包括受阻）不再被认作可保存/载入边界；请求只会在实际回源或权威确认路径正常清除。前述书签 P2 的源码缺口已关闭。

复查当前根目录生产链中与侦探调查、单选付费重演、正式解释及交接相关的整合部分，未发现另外可确认的新 P1/P2。人工剪枝 P1 的修复仍在：单模型候选与共同付费的来源剪枝/覆盖证书保持分离，正式解释不绕过竞争者排除。临时假设封装、当前候选资格、接触核对、连续门道唯一回执与严格揭幕路径未见本次书签修复引入回退。书签原生保存按钮位于已打开的案卷中，reading_changed 在打开时已通知 Main；非就绪请求继续由既有提示拒绝，不清空连续状态来强行保存。

本次确认限于静态机制接线；未读取设计、未改生产源码，未运行构建、正式会话、游戏或界面探针。实际回源、正式作答接受、章节交接及截图体验仍需各自证据，不能由本报告代替。

本次主要复核源码 SHA-256：

| 路径 | SHA-256 |
| --- | --- |
| godot_csharp/scenes/Host/Main.gd | C8DFB08018AD8E96AA99C235127848972F1FE85C439EBE13D9EE0D99BDB003FA |
| godot_csharp/scenes/Shared/Field/WorldView.gd | 3D73127EE49E5CB98BF7490B98931E095A8B7A4262BBA6C842CD21EED47F29B3 |
| godot_csharp/scenes/UI/UISystem.gd | 04E70E996B7B99AD543DF97A55270B42320077045673F87D1C56068C7A1B9B12 |
| godot_csharp/scenes/UI/TitleUI.gd | FED990AB0C51AD7F8CAABB0CAB17243DDDBCEFF7846A6C58899286A6F28F821B |
| language_core/30-session.dv | 5848CA5FE78FA9950D626C2BC98D596996C8F199A1F3C367E5F6F5F407889F7F |
| language_core/60-game.dv | BF1A09D8146E3227F749DD5D8F93F2A0A8E18BCD9E1C3B098F5631BEF5A61E4F |
## DialogueUI 称谓小改静态复核

仅核对当前根目录 DialogueUI.gd 及 Main._presentation_line、dvg_presentation 的称谓交付接口与 godot AGENTS 对应条目；未触碰冻结目录，未运行构建、导入或会话，不读取正在进行的盲玩记录。

此次修改未发现可确认新增 P1/P2：_speakers 字段及其写入/回退读取已删除；当前 speaker_label 与新加入的 _history 条目都只使用当前 line.speaker_name，空值保持空白，不使用内部 speaker ID 或之前的条目补称谓。非空已交付称谓仍原样进入当前显示与该条历史，不因删除缓存丢失。Main 仍逐行交付 result.speaker_name；dvg_presentation 按当前 viewer 的认识计算该字段。line_read 继续发出原始当前行字典，逐行来源未被替换。内部 speaker 仍只用于 _cast 立绘资源映射等用途，称谓修改没有切断原立绘查找。godot AGENTS 已同步说明当前/回看称谓只认本条 speaker_name。

这确认显示层不再把先前条目的称谓继承给本条空称谓，不代替不同观察者实际会话的运行验收。

DialogueUI.gd SHA-256：2B9C17BC5FE0EB5AD37A69D203E722886690874A7F62C400C59CB6B2FEDF2CD4。
## 新批次：重演准入缓存与本次鼠标事件命中

本节以根目录当前 30-session.dv / WorldView.gd 相对上一冻结副本的差异为范围，仅静态读取。未读取设计、未改生产源码、未运行会话，也未干预 blind03。

未发现可确认新增 P1/P2。重演准入现调用 dvs_replay_has_available_candidate(dvp_actor_view(actor, mind))，因此规划传入的显式 mind 被用于候选条件，而不是误读原 actor.mind。共同 helper 与 paid Complete 使用相同 topic、非空 basis、available_when 条件；空候选目录仅保留既有 factor-only 的付费约束入口。manual_available_when 仍在独立 dvs_manual_candidate_available 中重核，SelectPlayer、player_choice 与 Complete 的选中 candidate ID 守卫未被 common helper 替代；人工仍只选中一个模型，剪枝与覆盖仍共同计费。

缓存归属为完整 actor.mind 的不可变值，key 另包含 actor.id、完整候选目录、question_ref 的有无与值、topic_bindings 与 heard_topics。沿 dvp_topic→dvp_question / dvp_topic_scan / dvs_topic_was_heard 核对，外部依赖均在 key 内，本人事实/采纳/时钟等已在 owner mind 中。返回值只有 Bool；缓存不保存候选对象、现场资源、输入封装或执行结果，不发生付费、观察或正式提交副作用。mind 或上述输入变化不会命中旧输入的 Bool，显式预测 mind 与当前 mind 也不混用。缓存不证明某个手动 hypothesis 仍可选，后者继续在独立守卫核对。

pointer_hit(point) 对悬停和点击共用同一现场本地坐标查询：站点命中仍要求 site_visible，家具 fallback 仍要求 _player_object_drawn 与 in_sight，有映射站点时仍核对其可见性。调用 furniture_hits 前用 to_global(point) 恢复该适配器所需全局坐标。鼠标按下使用本次 event.position，经 field.get_global_transform_with_canvas().affine_inverse() 逆变换，站点交互与地面 _navigate 共用这一点；不再以旧 hovered 或之后读取的鼠标位置替换本次事件。原 enabled、field_active、_in_reach、当前选项与正式 can_submit 门均保留；命中辅助没有授予观察或扩大正式资格。宿主 AGENTS 已记录本地坐标与同次事件合同。

缩放/镜头变换、快速连续输入和真实遮挡的实际画面结果仍未运行验证。

复核指纹：30-session.dv = CAC4B05FA5B4D7F3DE66212AD74E3EC5366D626ECBDF93EDC682E5C45F26CC37；WorldView.gd = 9AD1673CB0B9DA92A7BC219F9AE8204C1E8DC12383470517D096852C57CC94A3。
## 05 源侧 released 取消重试证明独立静态复核

已读 continuous-cancel-host-05-implementation.md、continuous-cancel-retry-05-implementation.md，并沿当前根源码 Main / WorldView / 30-session 核对。未读取设计、未修改生产或隔离 runtime，未运行检查/会话，也未读取进行中的行为记录。

未发现本小改可确认新增 P1/P2。WorldView 先核对实际 departure_room 为源房，置 settling 后停止局部路线，发 daily blocked；只有 released、同图且 transitions 为空才附 source_cancel_candidate。该层没有签入 identity_verified 或行动事件。request_source_position 与执行开始坐标分列。

执行开始源绑定来自 DvFieldExecutor：源地图/房间取本次 execution_snapshot，核对 retained body 资源/地图及真实 departure_room 后才捕获 source_point/source_geometry；只接收 serial/who/action 均匹配的 daily 回执，再由 executor 覆盖填入 source_map/source_room/source_position/source_geometry。Main 的规范化沿此回执核对当前 Prepared move 非 station、宿主验证后的 continuous destination、自己的 request/actor/action/serial/action_event、snapshot.player_control 与现场玩家身份；同时核对 native_request settling/serial/action/released，地图/两房、空 transitions、有限坐标、source=actual=current body、身体已停止，以及当前重算的源几何。来件 continuous_cancel 先删除，不接受自报 identity_verified；只有独立匹配完成才写本次 action_event 的 source-cancel-v1。除主动移除自报证明再生成规范化证明外，原始回执字段（包括请求起点、executor 实际起点/终点及源几何）仍保留，没有把请求起点替换执行起点。

DV 外层仅接受 godot rejected；内部核对版本、daily blocked/released、非站点跨房 move、主体/行动/serial/request/外层 action_event、源房与地图、空 transitions，以及原始和规范化证明相同的有限二维 source/actual 坐标与合法 source_geometry。Main 的实际绑定校验是此跨层合同的一部分；DV 未另行读取 Godot 几何来伪造来源。

凭据只在失败主体等于 player_actor 时写入现有 manual_contact_retries[action.id]，绑定本次拒绝签名；仅临时 manual_input 且当前签名仍相同才绕过相同拒绝门。token 不自动提交动作，后续仍需新的正式人工选择，全部其他前提及成功次数门继续核对，自主不使用此 token。成功或后续同动作 Complete 覆盖清除。原动作仍走 rejected 费用/used/行动配额结算；未新增 completed_actions、地点写入、目标观察或答案，三语提示只在宿主显示取消。

严格适用范围以“Prepared 起点与实际停止点精确相等”为可静态确认的条件；坐标不同的真实返回、其他取消原因、跨图、持续受阻及缺字段旧证据都不因此获得此分类。是否在实际 released 输入中产生证明并允许下一次输入，仍需新版本行为证据。

复核 SHA-256：Main.gd = 5E38002A60F633008C783E631248FD8128B9A8342DD46294076D6F784EA73E62；WorldView.gd = 59AD5A9A51689ACF3A91506D0909F451658CE3ED0ED7CE6A80BCCD191DD138FC；30-session.dv = F05CFA420DBB99C7F97A921B990F8453C8521E3F012B3511992602BAD1B88F5A。
## 06 路径内任务去重前移静态复核

相对 candidate05 的 30-session.dv，当前生产差异仅为 dvp_plan 中 spatially_possible 后新增 `not dv_has(node.tasks, action.id)`，位于 dvp_eligible、费用时域检查、价值计算、plans 追加与 best 更新之前。该调整与原路径内非 transit 去重意图一致，未发现可确认新增 P1/P2。

沿 tasks 数据流核对：根节点 tasks=[]，唯一新增任务的位置仅在预测 applied 且 `not is_transit` 时 append；travel.from!=travel.to 的移动边不入 tasks。原后继入队的 `(not is_transit and dv_has(node.tasks, action.id))` 防御仍保留。因而前置检查排除的是原来不能继续入队、却已被重复计分并可能更新 best 的任务；合法 transit 重访不因此变成任务重复，真实会话的 repeatable 准入与 Complete 没有改动。

planning_limit/budget、expanded 的按节点扣法、depth_limit、lookahead_limit、节点/行动遍历顺序、费用与预测源保持原代码。预期改变的是删除非法重复路径的评分/plan 条目和相应 best，不能据此声称动作选择完全不变；真实动作选择正可能因不再采用虚高重复分而改变。重复任务的 time_blocked 诊断也会被前置过滤，这是该分支不再进入准入后的直接结果，不构成预算扩大。AGENTS 与 README 将其限定为路径内重复计分修正，与此前窗口分列，未声称解决所有再规划循环。

没有构建或运行；未读取设计或修改生产/冻结源码。再次正式规划是否停止重选、实际调查/推演/交接是否改善保持待验证。

30-session.dv SHA-256：366B285A8D1E0FBD2DF59AE485C4C062C77F085DE3302B7F084BFBDB7B73EE4E。
## 07 开放地板边界与阶段提示静态复核

已读 open-boundary-07-implementation.md，并比较当前根 WorldView / CampusMapView 与 candidate06 冻结副本。未读取设计、未改源码/旧冻结，未运行构建、会话或几何探针。静态未发现可确认新增 P1/P2，不据此宣布所有通行或画面验收。

passage_at 限制在当前已载入 map：walker 的实际 position 必须等于 source，departure_room/entered_room 均核对源房，attempted 必须是指定目标室内；两个 authored 地板格只允许四向相邻、`.`/`S`、短步不超过32，并保留 source/attempted 的原 walkable 检查与真实 CharacterBody2D.test_move。查询没有改位置、地图、导航格或碰撞；不是由 action ID 或虚拟门推断几何。

WorldView 在 _step 的本次越界 attempted 上查询，仅从 available、同图且目标执行房匹配的选项中取得唯一候选；_request_continuous 仍要求当前 _can_select、真实源房、heading 指向 landing，并保存原主体/行动/request/from/to/map。Main 现有独立 prepared 匹配保持，查询返回本身不能授权移动。prepared 前 _step 不跨到目标；prepared 后只给已绑定 to_room 放行，其他 next_room 不满足 admitted，非空请求也不发第二个跨房选择。真正位移仍用 move_and_slide，目标成功仍要真实 entered_room/walkable 与原唯一 Complete。此处确认逻辑请求门未扩大到任意第三房，不等于已用碰撞场景证明所有斜向滑动、边角、动态占位结果。

_near_door 与 _door_option/鼠标 landing 复用 passage_at，保留 D 分支与跨图几何原路径；开放边界同房/不同房与 execution_room fallback 一致，多义同目标动作不取首项。远距鼠标不满足短步条件时不会用该查询提前宣告跨界，仍由原局部路线走近，到真实边界再提交。

continuous_hint 只读取当前请求阶段/取消标记及语言，返回三语短句；没有世界读取、观察写入、动作/模型执行或正文事件。显示保留模态/对白/关闭/隐藏场抑制，清除请求即恢复原提示。此次差异未改 camera_point、相机边距或 SightMask；未知墙遮罩边带仅留工程提案，不能称屏边裁切/泄漏风险已修复。

后续需分别观察开放边界实际提交/到室、实体墙与第三房拒绝、E/鼠标唯一匹配、松键取消和阶段短提示。源码接通不证明上述行为均发生。

复核 SHA-256：WorldView.gd = 05BD6AC8F43E624B92684B4943153600B94CBEB35B73B5D4823D1E8B46546141；CampusMapView.gd = C6DC06DA8F772B2445350BCBE0CF3B64B936D0335AE30C7EBE02AA2E291E75F6。