# 空候选重演准入修正实施记录

2026-10-21。经主代理授权修改根工作区，未改隔离runtime，未构建/运行。独立证据诊断先封存于replay-no-solution-diagnosis.md；其中话题缺失已进一步核实为本窗口正式question.prompt尚未执行，不是收闻解析漏字段。

根30-session.dv：
- 提取dvs_replay_candidate_available，严格复用原Complete的topic+basis+available_when条件；不新增内容正确条件。
- replay准入要求dvs_replay_has_available_candidate；有目录时至少一个符合，短路返回。存在性结果只在完整mind上按actor id、候选目录、question_ref存在性和值、topic_bindings、heard_topics缓存；纯资格检查不运行模型、不收费，Complete仍逐候选当前资格重核，不为每个模型重复扫描整个目录。
- 保留没有候选目录而有factor_constraints的因子专用replay路径；无候选且无因子任务则不准入空重演。该分支不修改原因子求证/收费/预算/证书。
- 人工dvs_manual_candidate_available复用共同条件后仍核对manual_available_when；原人工选中id、输入签名、Complete重核保留。
- formal Complete候选循环改调用共同helper；其余模型执行、重查、前缀、预算与次数保持。

工程文档：language_core/AGENTS.md与README.md新增“重演准入”合同。没有更改问题开放、缺失线索、个人价值、目标/答案、费用或配额。

主代理静态复核指出eligible_base的显式mind可能是预测分支；已把调用改为dvs_replay_has_available_candidate(dvp_actor_view(actor,mind))，避免沿actor旧mind判准入。缓存依赖逐项核对：共同候选helper仅读完整candidate、actor.mind，以及dvp_topic所用question_ref、policy.topic_bindings、heard_topics；dvs_topic_was_heard只读heard_topics。key包含actor id、完整目录、问题引用存在性和值、完整绑定与收闻字段，并挂在该显式view的完整mind上；没有物理世界、历史事件、费用或执行副作用依赖。factor-only空目录分支不缓存。输入变化自然换mind/key，命中只返回Bool且无状态效果，不跨保存继承运行时memo。

静态复读了三个调用边界与缓存输入，未做编译/语法/会话验证；不能声称下个窗口正确推演或通关。待主代理集中工程检查与新独立证据验证。该修正改变空重演准入，须与旧轮行为条件分列，不作同条件性能结论。
