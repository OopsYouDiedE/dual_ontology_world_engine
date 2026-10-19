# Gemini 性能捷径只读机制审计

审计时间：2026-10-19 07:50—08:00 +08:00。HEAD：1940084fa488cda882b43d63358ae439b492c37f。

范围：按主代理委派读取工程约束、生产源码和 Git 差异；未读取任何项目设计文件、未读附件正文、未修改源码、未构建、未运行探针或正式会话。本报告只给源码事实与静态后果，设计取舍由主代理承担。工作区有其他代理同步编辑，以下定位描述对应实际读取片段，不能当作冻结快照；文末 SHA 在07:58集中读取，无法追认此前并发修改前的字节。

## 生产链与接口

`game/entry.dv` 明确 include 五份 core、五份 verification/session，以及 chapter1 与 chapter2 的 content/daily/dialogue/homecoming。`dvg_extend` 从当前 state 依序 c2_extend_scene → c2d_install_scene → c2daily_install_scene → c2h_install_scene，再配置运行政策；它保留事件与实际状态，内容可用事件不代表几何到达。

`dvg_prepare` 调用 dvs_schedule → dvg_refresh_goals → dvs_prepare_actor；pending 或 awaiting_player 阻止新意图。`dvg_complete` 调用 dvs_session_complete 后扩章。`Session.cs` 的 SessionPrepare/Complete/Close 直接桥接这些函数。正式逻辑适配 `debug-run.dv` 也调用相同 prepare/complete，回执 executor=formal-model，使用 pending.model.actual；它没有 Godot 几何证据。

目标刷新仅处理调度选中的 cursor 主体，无 scheduled_action，且本人 mind.revision/time 或章号改变时才执行；第二章调用 c2h_refresh_actor(c2d_refresh_actor_goals(actor))，随后 known_future_wait。候选与目标限制要沿此实际调用链核对，不能只搜索某个历史函数名。

## 已确认的行为缩减

### 第二章深度与提前停止

`ab76bfd7` 在 `chapter2/content.dv` 的 `c2_extend_actor_from` 把 plan_depth=2 改为1，first_night_plan_depth=2改为1，positive_move_root_stop=false改为true，daytime_wait_chunk_limit=120改为180。该片段实际位于 content.dv（初读1669），不是 daily.dv。daily.dv 本次提交只给 guard 选择补 owned_decision/effects 等接线，没有该深度赋值。

消费者 `15-action-planning.dv:dvp_plan` 读取 plan_depth；只有 depth+1<depth_limit 才 forecast 并加入后继层。深度1下此条件从根层即为false，真实选择只比较单步候选，不能把它描述为保持两层搜索的纯计算优化。正向移动若取得 achievement，可进入 current_action；根层 current_action 非false即清空 next_frontier、stop=current-affordance-root，因此仅把 plan_depth 恢复为2也不必然恢复后继比较。

`chapter1/opening.dv:c1n_known_future_wait_actor` 另有首夜 plan_depth=1 的动态机制并保存/恢复 first_night_plan_depth。第二章把恢复值也写成1，需同步核对这一消费者。该文件的 daytime_wait_chunk_limit 消费处会将白天等待片段上限放宽到180，并仍按目标边界截断；这改变实际行动片段/观察机会，不能只解释为降低调用次数。

### 目标与可用导航

`ab76bfd7` 从 `c2m_goals` 尾部删除 `c2m_observation_goal_templates(actor.id,c2m_fields())` 追加循环。定义仍保留，当前检索没有生产调用，但结论依据的是已删除的追加语句与 `c2_personal_goals → c2i_goals → c2m_goals` 消费链，而非名称缺失。其他证据目标仍存在：c2i_goals 的地点字段检视循环、c2_personal_goal_templates 的时钟/通路/天气等声明继续生成；不能据此声称“全部观察被删除”，也不能据剩余目标推定被删集合等价。

同提交把私下任务的 travel_when 从 motive（原有个别额外时窗）改为 when=available∧motive。这会缩小导航目标出现条件。服务目标增加 navigation_requirements/methods，也改变导航资格。应与实际首步、已知来源及真实到达分别核对。这些都是内容行为条件变更，不是数据结构等价替换。

同提交删除安全采样中 actor.mind.time-read_claim.time>30 的失效条件、删除 recheck 对 seen.value=false 的要求、删除最低证据时间 sample.time+30。即使环境 epoch 校验保留，也不能据此宣称原新鲜度合同完全未变。具体应保留何种规则交主代理与负责内容的代理处理。

扩章还新增 hypothesis=false、argument=false、simulations=[]，原先直接继承这些值。是否应重置需要设计与本章模型合同依据；静态上它确实丢弃前章的当前模型/论证状态，不能归类为只减少序列化。

### 预算与候选：本次没有发现的新事实边界

c1_policy 提供 budget=240、reason/model/plan/dependency各320。`30-runner.dv` 开局保存 planning_limit；每次自主准备恢复 plan_budget=planning_limit，选择后减去 choice.plan_expansions，另记 dependency_work_total。`15-action-planning` 的 dependency_budget 与 plan_budget 分开；深度/停止条件会改变实际使用量，但本次最近提交差异没有显示这些数值预算统一减少。不能把 plan_depth 缩减说成预算未变因而行为不变。

`dvp_room_actions` 按 room 与 action required_room/travel.from 缓存保序目录切片，未知房间回全目录；节点内仍执行 spatially_possible 与 dvp_eligible。这一实现是位置资格过滤，单凭候选数量降低不能认定其删除合法候选。若要证明等价，需逐字段依赖和中性差分；本次未运行。

## 证据裁剪与当前修复状态

`ab76bfd7` 新增 Runtime.JsonContext，在 WriteJsonFast 识别二元数组字段对：无条件清空 history/simulations；事件内清空 adoptions/source_adoptions、world/simulation/predicted/trace/catalog，以及特定 model_argument_unavailable 的 competitors/factor_coverage。`2a1ab5de` 又增加 actions/departure_goals/first_night_base_goals/hearing_grants。这些语句确实把原值写为[]，影响 json_stringify/json_print/json_write；是丢证据，不是无损结构共享。

当前未提交 Runtime.cs 差异已移除 JsonContext 与上述过滤，WriteJsonFast 逐项递归写入，精确数仍写原字符串；此为源码观察，未构建验证。不能继续用历史提交的“232.5s全五日”标题作为恢复完整证据后的速度结论。

仍有独立裁剪：`30-runner.dv:dvs_session_close`（初读825附近）创建 pruned_actors，逐个 `dv_without(actor,"simulations")`，返回 final.actors=pruned_actors。Git 追到 `2704fb2b` 新增。因此仅恢复 Runtime 写出器仍不使 close trace 的 final actors 保存完整模拟缓存。`Session.Trace` 与 `WriteTrace` 都调用 SessionClose，前者经 Runtime JSON，后者经 System.Text.Json；两者都会收到已剪过的 final actors。`WriteSave` 直接序列化 live state，保留该字段。JSON debug 最后只输出 close trace；DAG最后 checkpoint 明确包含 trace 与 last_live_session，后者保留 live state。保留位置不一致必须明确，不能用 Save 的存在替代 Trace 完整性说明。

`10-subjective.dv:dvs_event` 先 rt_snapshot，再 rt_raw_event，再推入 events：原始 DAG提交发生在debug投影之前。`debug-run.dv` projection_every>1只允许dag，按批刷新玩家正文与debug_events，正常结束刷新尾部。中断时尾部投影可能未刷新，应如实记录。

`95-model-argument.dv` 的 debug projection 省略 world/predicted、将部分大字段替换引用，输出 raw_event_ref，注释明确依赖原值。它是显式投影；只有对应完整 raw 值可解析时才是可追溯证据，不能把投影自身称为完整快照。

私有规划 `dvp_forecast` 清空预测输入 history/seen，规划memo索引也剥离mind审计日志；这些处理不直接改真实state，且真实节点另有路径seen去重。其等价性边界是所调用预测/评分不得读取被剥离日志，不能因“private”标签自动证明。该代码属于此前优化，和 Runtime 全局证据清空应分开审计。

## 审计结论及未验证范围

已确认需要主代理处理的工程问题是第二章搜索深度/根层停止/等待上限的行为变化，以及关闭Trace主动剔除simulations。目标、新鲜度与扩章模型重置涉及内容/认知规则，已报告主代理，不由本审计改动。没有执行正式一级、UI或三级验收；本报告不提供任何人物、剧情、五日完成或耗时改善结论。

## SHA-256 读时记录（2026-10-19 07:58 +08:00）
- language_core/verification/session/10-subjective.dv: C228A75A4347899959DD1467A803DF216FD65E2A4EA4C327AEB3BF285A7E7C20
- language_core/verification/session/15-action-planning.dv: 667474DFD78D42EEB04DF25BC367027A0A5FFEF29473F0435EF768087CD82EAB
- language_core/verification/session/25-simulation.dv: 3B23E90A98E9B3B7ACEFDE3BF251A7CE3BA47169542169A600C42A242F8B4B2D
- language_core/verification/session/30-runner.dv: 2C9A5AB8EC7FB07950ED19C882838B36E32413180435DDC130E42AFCD2EF2890
- language_core/verification/scenarios/chapter2/daily.dv: 843D36CC69E2AAA77584749A2288EE655B9B154F68CBB89F0C23D91BD04DB3A2
- language_core/verification/scenarios/chapter2/content.dv: 9CC86DFD0462D241786721AAB2BF864D712C7729020CEF4FA9FA6DC05FCA309E
- language_core/verification/scenarios/chapter2/dialogue.dv: 2BDBF1CC3B1C4882FE19A099CB632057E91770E7C4226AAFB96862BE618F845D
- language_core/verification/scenarios/chapter1/content.dv: 39C1AB4B9755F725EE5E2F54BB14A50D4AA6B4C56FF20BCEE0D2700174D8929B
- language_core/verification/scenarios/chapter1/opening.dv: F8AF6DF8873AAE00520C059F72D78997BC2D8992004E984699B246D73399AAAE
- language_core/game/entry.dv: B626A94838193271DC8AB6D3244C36359BF1DC7396803CC571406A09C9541D46
- language_core/game/debug-run.dv: 9F21FA451B14899D49D9C37363B78A1409ACE7CEAD81B2D42769E12515681736
- language_core/compiler/Runtime.cs: D690520A441215F3428FB2E3069C50970AF7D34DF716E3749D790F858014D75B
- language_core/Session/Session.cs: D08424D7CF238691FD2DA90BDD944AA7BD81064672E4F44F4D1F33A8091D1D2C
- language_core/core/95-model-argument.dv: F06B5C8FB8A19E3FC07128746914ADFA89F821F06F47CAFE9C21A1F897CF0159
