# 静态审计（2026-10-16）

本记录只读源码；未读取设计规范，未执行编译、构建、会话、测试。源码正在由其他代理修订，行号以本次读取为准。排除 personal-choice、action-planning 与 chapter1-departure，由其他代理负责。

## 配额、世界资源与停止

- game/campaign.dv:13、27 的 horizon 是绝对世界分钟 7200，duration_budget 为 7200-run_start；不是从开局额外经过7200分钟。扩章保持演员、账本、事件及剩余配额。
- scenarios/chapter1-content.dv 的 c1_player_policy 当前声明 budget=48、reason_budget/model_budget/plan_budget=320。同文件 budget_scope 明示侦探48来自来源，NPC48属工程终止；不应将全部48笼统当作可任意调整的调试上限。scenarios/chapter1-campaign.dv:22 将本人行动配额求和为 max_actions。
- session/30-runner.dv:531、544 每次 Complete 扣本人 budget 并增加 executed，包括执行失败回执；因此配额计实际完成尝试，而不是只计成功。balance/ledger 是真实行动报价结算余额，独立于上述完成次数。
- runner:299、325 把 plan_budget 从 opening planning_limit 恢复为单次搜索上限，扣本次展开，并累计 planning_work_total。reason_budget 与 model_budget 为沿会话扣除的认知/重演余额；25-simulation.dv:281-282 另有 model_work_remaining/limit，不能视作世界时间。
- game/debug-run.dv:10-13 的 max_steps 是适配器完成次数上限，false继承 state.limit。tools/run_debug.py 的 timeout 默认600现实秒，是进程保护；超时属于证据不完整，不能报告剧情结束。
- runner:172 active 同时受世界时间、全局次数及空闲/队列条件限制；close:567-576 将 duration、本人耗尽及无动作混合为 budget-exhausted / no-admissible-action-or-budget，丢失具体停止原因。

建议保留现有有限配额，避免为了五日目标抬高侦探48、认知320、账本余额或扩章重置。现有界限不足以保证世界时钟到第五日，不能作该保证。可先把停止原因明确分成 world-horizon-reached、action-window-exhausted、actor-action-quota-exhausted、no-admissible-action、host-step-window、host-timeout，并附 executed/limit、各主体剩余配额、队列长度、当前时刻/horizon；不同原因可同时保存，主原因采用明确优先级。这不改变决策条件或资源。

若另行授权工程保护配额，建议在会话层单列 engineering_completion_limit，与来源约束的 policy.budget 完全分离，初值保持现有 max_actions；扩大它本身仍不会绕过主体来源配额。不能无来源选一个更大常数宣称足够五日，也不能以零成本循环对应任意最小时间推导有限次数。要求真正保证世界第五日需先证明行动时长下界和无动作时世界推进合同；本审计无设计依据，不作该语义修订。

## 记录类型边界

core/98-debug.dv:7 以 rt_is_list(raw_action) 即作为记录，:9 dv_find(action,"id",...)。普通非空列表（如字符串列表）会进入 Runtime.cs:63 的 Items(entries[i])[0]，造成类型/索引异常。建议改为 dv_record_like(raw_action) 后才字段查询；非记录保留标量/列表展示或明确校验失败。当前所阅生产事件 action 由记录或字符串构成，未发现实际普通列表生产者；这是通用投影输入边界漏洞，不能声称本轮会话已触发。

排除误报：core/20-observation.dv:34 在投影普通列表前已用 dv_record_like；26-model-slicing.dv:30 返回 false 模型后立即检查并 return；dvs_cached_model 返回至少含 fit/status 的记录；dvs_argument_valid 明确拦截 false。未发现上述路径存在本轮可证实的 false→dv_find 漏洞。不修改 dv_has_key/dv_find 的底层合同，以免将错误输入静默当作缺失字段。

## 本轮 diff 接线静态复审

审读目标为 completed_actions、规范收闻/决定、广播链及投票，继续不读设计、不运行编译或会话。

- runner 的 completed_actions 只在 applied 后追加 action.id，owner_requirements 的 completed_action_requirement 实际读取同一主体的该列表；不是无消费者字段。广播数组的前一项被后续动作消费，依赖的是成功完成记录，不是尝试列表 used。
- c2_norm_actions 的宣读 speech.norm_proposal 与读板 action.norm_receipt 分别在20-speech的真实收闻路径及成功Complete路径进入 dvs_receive_norm，写入以 norm.id 为键的 norm_proposals。norm_decision 同时被正式资格过滤及成功完成函数读取，内容逐值核对；成功决定才安装/移除本人的 policy.norm_rules。该字段链不是仅声明而无消费者。
- 第五日票数和本人voted字段由ballot行effects交给正式row law；可选提案要求本人的 accepted.<id>，该字段由 c2_decide_norm_law 在实际动作执行后写入。签署仍要求本人已投票；council_end环境法则要求所有signed字段，不由展示提案或选出候选直接设为完成。新的council议事目标使用已知提案正文作机会门槛，正文知识与接受决定是两个来源。
- 未在所读diff发现确定性的新增编译或类型崩溃点。这个结论限于所列数据生产/消费路径，不表示运行验收。

发现一个可确定的证据字段错配：runner 的 parent 在 action_execution 后若产生observation会改为观察事件ID，随后调用 dvs_complete_norm(..., parent)。该函数 stance.execution_event 直接取 parent；规范决定动作有观察grants，因而正常路径会把 observation ID写成execution_event。建议runner独立保存动作执行事件ID，给norm决定记录另传该值；若仅需记录当前认知因果父则应使用明确的cause_event字段，同时保留可连回实际执行的ID。该问题不会自行制造接受或导致崩溃，但会误导精确来源审查。已即时通知主代理。
