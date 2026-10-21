# 可读比较落地记录

经主代理解除本区域冻结后，限定应用原提案中 language_core/40-chapter1.dv 与 60-game.dv 两个文件的 hunks。未应用 AGENTS/README 文档 hunks，交由主代理与另一项改动统一整合。未运行构建、语法检查、模型、游戏或会话。

40 修改 c1_hist_feature/features 并新增相邻 c1_hist_public_feature 白名单。60 新增 dvg_replay_comparison，只在 dvg_replay 追加可读行与 comparisons 元数据。主代理的 dvg_location_title/hud.room 改动已静态确认仍在；人工假设 Agent 已接管标题与记录优先展示，不应覆盖本次比较行。

原提案将观察依赖错误命名为 observation_events。落地已修正为 observation_action_ids：20-core.dv:dv_perceive 构造 dependency=[event_id,local_ref]、id=[event_id,subject,local_ref]；30-session.dv:dvs_session_complete 传入 pending.action_event，而 dvs_dispatch_due 将该值构造为 action+state.executed（例如 actionN）。它是正式回执关联动作标识，不是后来 action_execution/observation 的审计事件 ID。字段不冒称观察事件，也不从 dependency 推断或构造审计事件 ID。

筛选只接受本人 seen 的 perception claim，ref/value 对应观察，与当次和当前无争议 cell 完整一致，trust/confidence > 0；额外核对 dependency 长度2、claim.id长度3、首项一致、依赖 ref 对应且首项不等于字面 inference。故 inference 来源或其字面依赖不会被冒充为观察来源。source_refs 继续保持原假设 basis，model_event 保持已有计算事件；没有读取权威世界或 raw assumptions。

原始提案、指纹和报告保留原字节，故原提案中的 observation_events 名称已经由本落地记录更正；后续应以生产源码中的 observation_action_ids 为准。未验证编译或实际显示效果。
