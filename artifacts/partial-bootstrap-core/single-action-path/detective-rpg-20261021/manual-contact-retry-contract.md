# 人工接触重试候选合同（未应用）

候选：manual-contact-retry-candidate.patch。仅生成审阅材料，生产源码未改；未执行构建或会话。`git apply --check` 仅核对候选与当前文本可匹配，不证明 DV 编译或行为通过。

现有回执依据：
- Main.gd:704、708：顶层 executor=godot、outcome=rejected；field.executor=dv-world、contact=false、site 为具体交互点。这能区分真实接触失败与 outcome=applied 后 DV 自己拒绝。
- WorldView.gd:914–928：有活动运动job时附 station、approach_point、station_point、station_radius、arrival_tolerance、actual_position、arrival_error；905/911 的实际受阻通过 daily blocked 返回。缺资源/缺站点/无可达路径的早退没有完整job证据。
- Main.gd:1299、1305：身份/站点不匹配写入 reason，候选不接受有 reason 的运动回执。候选同时核对 who/action。DV 不读取现场坐标当认知事实。

新增 actor.manual_contact_retries：按 action id 映射到 false 或 {failure_class, action_event}。
- 仅由正式 Complete 在执行失败且 actor.id == state.player_actor 时写入。
- 分类只接受 contact-out-of-reach、native-motion-blocked；formal-model、资源/身份/站点证明错误、DV权威前提拒绝不会生成。
- 同一行动的后续 Complete 总是覆盖此项；成功清 false。其他行动不消费该项。
- 仅临时 manual_input 视图读取；它允许显式人工候选/选择重试绕过同签名拒绝，不绕过 precondition、owned/normative/personal admission、成功一次限制、正式报价或 Complete。
- 不进 actor.mind、observation 或 public_grants；不是预测事实，不改自主主体选择。
- 此字段属于 actor 正式控制状态，需要存档、恢复、扩章保持；既有 actor 字段复制方式应保留未知字段。旧存档默认 []；若旧存档只有 used 无 completed_actions，不可凭空把旧失败当成功或释放已成功动作，需维持当前存档兼容合同并单列旧版迁移策略。

成功限制：候选把 eligible_base 内部 used 局部变量的读取改为 completed_actions，因此 once、默认非repeat以及非repeat speech/response 的“已经做过”限制以成功为准；外层 used 原样保留全部尝试，费用/预算扣减原样不改。请求上下文与 answer/replay/inspection 自身签名保持原路径。

限制与待主代理决策：
- 候选只允许已有完整真实job证明的运动失败；早期无路线而没有job证明的回执仍不人工放开。若要放开，须先让宿主给出可信且明确的 failure_class，不能根据 action 名称猜测。
- 字段持久化后，在人工重新打开同一动作选择时仍允许重试，但每次仍需真实输入。没有自动重试、没有自动清拒绝签名。
- 候选没有调整规划层数、预算、费用、剧情前提与结局。
- 若主代理采用，应同步 language_core/AGENTS.md 与 Session/README.md 的控制重试/一次成功合同，再集中工程排错及授权实际验证。